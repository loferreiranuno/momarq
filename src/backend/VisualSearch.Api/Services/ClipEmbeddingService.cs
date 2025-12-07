using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace VisualSearch.Api.Services;

/// <summary>
/// Service for generating CLIP embeddings from images using ONNX Runtime.
/// Used for server-side embedding generation during product image ingestion.
/// </summary>
public sealed class ClipEmbeddingService : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly ILogger<ClipEmbeddingService> _logger;
    private readonly bool _isModelLoaded;
    private readonly string? _inputName;
    private readonly string? _outputName;
    private readonly int _embeddingDimension = 768;

    // CLIP ViT-L/14 image preprocessing constants
    private const int ImageSize = 224;
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] Std = [0.26862954f, 0.26130258f, 0.27577711f];

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipEmbeddingService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="environment">The hosting environment.</param>
    public ClipEmbeddingService(ILogger<ClipEmbeddingService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;

        // var modelPath = Path.Combine(environment.ContentRootPath, "Models", "clip-vit-base-patch32-visual.onnx");
        var modelPath = Path.Combine(environment.ContentRootPath, "Models", "clip-vit-large-patch14-visual.onnx");
        if (File.Exists(modelPath))
        {
            try
            {
                var fileInfo = new FileInfo(modelPath);
                _logger.LogInformation("Found CLIP model file at {ModelPath}, size: {Size} bytes", modelPath, fileInfo.Length);

                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    InterOpNumThreads = 4,
                    IntraOpNumThreads = 4
                };

                _session = new InferenceSession(modelPath, sessionOptions);

                // Detect input/output names from the model
                _inputName = _session.InputMetadata.Keys.FirstOrDefault() ?? "pixel_values";
                _outputName = _session.OutputMetadata.Keys.FirstOrDefault() ?? "image_embeds";

                // Try to determine embedding dimension from output metadata
                var outputMeta = _session.OutputMetadata.Values.FirstOrDefault();
                if (outputMeta?.Dimensions is { Length: >= 2 })
                {
                    var lastDim = outputMeta.Dimensions[^1];
                    if (lastDim > 0)
                    {
                        _embeddingDimension = lastDim;
                    }
                }

                _isModelLoaded = true;
                _logger.LogInformation(
                    "CLIP model loaded successfully from {ModelPath}. Input: {Input}, Output: {Output}, Embedding dim: {Dim}",
                    modelPath, _inputName, _outputName, _embeddingDimension);

                // Log all input/output names for debugging
                _logger.LogDebug("Model inputs: {Inputs}", string.Join(", ", _session.InputMetadata.Keys));
                _logger.LogDebug("Model outputs: {Outputs}", string.Join(", ", _session.OutputMetadata.Keys));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load CLIP model from {ModelPath}. Server-side embedding generation will be disabled.", modelPath);
                _isModelLoaded = false;
            }
        }
        else
        {
            _logger.LogWarning("CLIP model not found at {ModelPath}. Server-side embedding generation will be disabled. Download the model and place it at this location.", modelPath);
            _isModelLoaded = false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the CLIP model is loaded and available.
    /// </summary>
    public bool IsModelLoaded => _isModelLoaded;

    /// <summary>
    /// Generates a 768-dimensional CLIP embedding from an image byte array.
    /// </summary>
    /// <param name="imageBytes">The raw image bytes (JPEG, PNG, etc.).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A normalized 768-dimensional embedding vector, or null if the model is not loaded.</returns>
    public async Task<float[]?> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (!_isModelLoaded || _session is null || _inputName is null || _outputName is null)
        {
            _logger.LogWarning("CLIP model not loaded, cannot generate embedding.");
            return null;
        }

        try
        {
            // Load and preprocess image
            using var image = await Image.LoadAsync<Rgb24>(new MemoryStream(imageBytes), cancellationToken);
            var tensor = PreprocessImage(image);

            // Run inference with detected input name
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, tensor)
            };

            using var results = _session.Run(inputs);

            // Find output by detected name
            var outputResult = results.FirstOrDefault(r => r.Name == _outputName) ?? results.First();
            var output = outputResult.AsTensor<float>();

            // Extract embedding - handle both [1, dim] and [dim] shapes
            var embedding = new float[_embeddingDimension];
            var shape = output.Dimensions.ToArray();

            if (shape.Length == 2)
            {
                // Shape [1, embedding_dim]
                for (int i = 0; i < _embeddingDimension && i < shape[1]; i++)
                {
                    embedding[i] = output[0, i];
                }
            }
            else if (shape.Length == 1)
            {
                // Shape [embedding_dim]
                for (int i = 0; i < _embeddingDimension && i < shape[0]; i++)
                {
                    embedding[i] = output[i];
                }
            }
            else
            {
                _logger.LogWarning("Unexpected output tensor shape: {Shape}", string.Join("x", shape));
                // Try to flatten and take first N values
                var flatOutput = output.ToArray();
                Array.Copy(flatOutput, embedding, Math.Min(flatOutput.Length, _embeddingDimension));
            }

            NormalizeVector(embedding);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate CLIP embedding.");
            return null;
        }
    }

    /// <summary>
    /// Generates a CLIP embedding from an image URL.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to process.</param>
    /// <param name="httpClient">The HTTP client for downloading the image.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A normalized 768-dimensional embedding vector, or null if generation fails.</returns>
    public async Task<float[]?> GenerateEmbeddingFromUrlAsync(string imageUrl, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        try
        {
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
            return await GenerateEmbeddingAsync(imageBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download image from {ImageUrl}", imageUrl);
            return null;
        }
    }

    /// <summary>
    /// Preprocesses an image for CLIP ViT-B/32 inference.
    /// </summary>
    private static DenseTensor<float> PreprocessImage(Image<Rgb24> image)
    {
        // Resize to 224x224 with center crop
        image.Mutate(x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(ImageSize, ImageSize),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));

        // Create tensor [1, 3, 224, 224]
        var tensor = new DenseTensor<float>([1, 3, ImageSize, ImageSize]);

        // Normalize pixels: (pixel / 255.0 - mean) / std
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < ImageSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < ImageSize; x++)
                {
                    var pixel = row[x];

                    tensor[0, 0, y, x] = (pixel.R / 255f - Mean[0]) / Std[0]; // R
                    tensor[0, 1, y, x] = (pixel.G / 255f - Mean[1]) / Std[1]; // G
                    tensor[0, 2, y, x] = (pixel.B / 255f - Mean[2]) / Std[2]; // B
                }
            }
        });

        return tensor;
    }

    /// <summary>
    /// L2-normalizes a vector in-place to unit length.
    /// </summary>
    private static void NormalizeVector(float[] vector)
    {
        float sumSquares = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        var magnitude = MathF.Sqrt(sumSquares);
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }
    }

    /// <summary>
    /// Generates a simple color-histogram based pseudo-embedding when CLIP model is not available.
    /// This is a fallback for demo/POC purposes - produces embedding vector from image colors.
    /// </summary>
    public async Task<float[]> GenerateFallbackEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(new MemoryStream(imageBytes), cancellationToken);
            
            // Resize to small size for fast processing
            image.Mutate(x => x.Resize(64, 64));

            // Create embedding from color statistics using current dimension
            var embedding = new float[_embeddingDimension];
            
            // Compute color histograms (3 channels × 64 bins = 192 values)
            var rHist = new float[64];
            var gHist = new float[64];
            var bHist = new float[64];
            
            int pixelCount = 0;
            float avgR = 0, avgG = 0, avgB = 0;
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        rHist[pixel.R >> 2]++;
                        gHist[pixel.G >> 2]++;
                        bHist[pixel.B >> 2]++;
                        avgR += pixel.R;
                        avgG += pixel.G;
                        avgB += pixel.B;
                        pixelCount++;
                    }
                }
            });

            // Normalize histograms and copy to embedding
            for (int i = 0; i < 64; i++)
            {
                embedding[i] = rHist[i] / pixelCount;
                embedding[64 + i] = gHist[i] / pixelCount;
                embedding[128 + i] = bHist[i] / pixelCount;
            }

            // Add color averages and spatial features
            avgR /= pixelCount;
            avgG /= pixelCount;
            avgB /= pixelCount;
            
            embedding[192] = avgR / 255f;
            embedding[193] = avgG / 255f;
            embedding[194] = avgB / 255f;

            // Fill remaining dimensions with hash-like values for variety
            var hash = 0;
            for (int i = 0; i < Math.Min(imageBytes.Length, 1000); i += 10)
            {
                hash = (hash * 31 + imageBytes[i]) % 10000;
            }

            var rng = new Random(hash);
            for (int i = 195; i < _embeddingDimension; i++)
            {
                embedding[i] = (float)(rng.NextDouble() * 0.1 - 0.05); // Small random values
            }

            NormalizeVector(embedding);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate fallback embedding");
            // Return random normalized vector as last resort
            var rng = new Random();
            var embedding = new float[_embeddingDimension];
            for (int i = 0; i < _embeddingDimension; i++)
            {
                embedding[i] = (float)(rng.NextDouble() * 2 - 1);
            }
            NormalizeVector(embedding);
            return embedding;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _session?.Dispose();
    }
}
