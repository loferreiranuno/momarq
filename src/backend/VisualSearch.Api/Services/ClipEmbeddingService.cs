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

    // CLIP ViT-B/32 image preprocessing constants
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

        var modelPath = Path.Combine(environment.ContentRootPath, "Models", "clip-vit-base-patch32-visual.onnx");

        if (File.Exists(modelPath))
        {
            try
            {
                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    InterOpNumThreads = 4,
                    IntraOpNumThreads = 4
                };

                _session = new InferenceSession(modelPath, sessionOptions);
                _isModelLoaded = true;
                _logger.LogInformation("CLIP model loaded successfully from {ModelPath}", modelPath);
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
    /// Generates a 512-dimensional CLIP embedding from an image byte array.
    /// </summary>
    /// <param name="imageBytes">The raw image bytes (JPEG, PNG, etc.).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A normalized 512-dimensional embedding vector, or null if the model is not loaded.</returns>
    public async Task<float[]?> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (!_isModelLoaded || _session is null)
        {
            _logger.LogWarning("CLIP model not loaded, cannot generate embedding.");
            return null;
        }

        try
        {
            // Load and preprocess image
            using var image = await Image.LoadAsync<Rgb24>(new MemoryStream(imageBytes), cancellationToken);
            var tensor = PreprocessImage(image);

            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("pixel_values", tensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            // Extract and normalize embedding
            var embedding = new float[512];
            for (int i = 0; i < 512; i++)
            {
                embedding[i] = output[0, i];
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
    /// <returns>A normalized 512-dimensional embedding vector, or null if generation fails.</returns>
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
    /// This is a fallback for demo/POC purposes - produces 512-dim vector from image colors.
    /// </summary>
    public async Task<float[]> GenerateFallbackEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(new MemoryStream(imageBytes), cancellationToken);
            
            // Resize to small size for fast processing
            image.Mutate(x => x.Resize(64, 64));

            // Create 512-dim embedding from color statistics
            var embedding = new float[512];
            
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
            for (int i = 195; i < 512; i++)
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
            var embedding = new float[512];
            for (int i = 0; i < 512; i++)
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
