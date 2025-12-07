using Pgvector;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Services;

/// <summary>
/// Facade service that combines object detection and CLIP embedding generation
/// for the visual search vectorization workflow.
/// </summary>
public sealed class VectorizationService
{
    private readonly ClipEmbeddingService _clipService;
    private readonly ObjectDetectionService _detectionService;
    private readonly ImageUploadService _imageUploadService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VectorizationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorizationService"/> class.
    /// </summary>
    public VectorizationService(
        ClipEmbeddingService clipService,
        ObjectDetectionService detectionService,
        ImageUploadService imageUploadService,
        IHttpClientFactory httpClientFactory,
        ILogger<VectorizationService> logger)
    {
        _clipService = clipService;
        _detectionService = detectionService;
        _imageUploadService = imageUploadService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether vectorization is available (CLIP model loaded).
    /// </summary>
    public bool IsAvailable => _clipService.IsModelLoaded;

    /// <summary>
    /// Gets a value indicating whether object detection is available (YOLO model loaded).
    /// </summary>
    public bool IsDetectionAvailable => _detectionService.IsModelLoaded;

    /// <summary>
    /// Generates a CLIP embedding for a single image.
    /// </summary>
    /// <param name="imageBytes">The image bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A 512-dimensional embedding vector, or null if generation fails.</returns>
    public async Task<float[]?> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (!_clipService.IsModelLoaded)
        {
            _logger.LogWarning("CLIP model not loaded, using fallback embedding.");
            return await _clipService.GenerateFallbackEmbeddingAsync(imageBytes, cancellationToken);
        }

        var embedding = await _clipService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
        return embedding ?? await _clipService.GenerateFallbackEmbeddingAsync(imageBytes, cancellationToken);
    }

    /// <summary>
    /// Generates a CLIP embedding from an image URL.
    /// </summary>
    /// <param name="imageUrl">The URL of the image.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A 512-dimensional embedding vector, or null if generation fails.</returns>
    public async Task<float[]?> GenerateEmbeddingFromUrlAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

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
    /// Vectorizes a single product image and updates its embedding.
    /// Reads from local file if available, otherwise downloads from URL.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="productImage">The product image to vectorize.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if vectorization succeeded, false otherwise.</returns>
    public async Task<bool> VectorizeProductImageAsync(
        VisualSearchDbContext dbContext,
        ProductImage productImage,
        CancellationToken cancellationToken = default)
    {
        float[]? embedding;

        // Prefer local file if available (no network/SSL issues)
        if (!string.IsNullOrWhiteSpace(productImage.LocalPath))
        {
            var imageBytes = await _imageUploadService.ReadImageAsync(productImage.LocalPath, cancellationToken);
            if (imageBytes is null)
            {
                _logger.LogWarning("Local file not found for product image {ImageId}: {LocalPath}",
                    productImage.Id, productImage.LocalPath);
                return false;
            }

            embedding = await GenerateEmbeddingAsync(imageBytes, cancellationToken);
        }
        else
        {
            // Fallback to URL download
            embedding = await GenerateEmbeddingFromUrlAsync(productImage.ImageUrl, cancellationToken);
        }

        if (embedding is null)
        {
            _logger.LogWarning("Failed to generate embedding for product image {ImageId}", productImage.Id);
            return false;
        }

        productImage.Embedding = new Vector(embedding);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vectorized product image {ImageId} (source: {Source})",
            productImage.Id,
            string.IsNullOrWhiteSpace(productImage.LocalPath) ? "URL" : "local");
        return true;
    }

    /// <summary>
    /// Vectorizes all images for a product.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="productId">The product ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of successfully vectorized images.</returns>
    public async Task<int> VectorizeProductAsync(
        VisualSearchDbContext dbContext,
        int productId,
        CancellationToken cancellationToken = default)
    {
        var images = dbContext.ProductImages
            .Where(pi => pi.ProductId == productId)
            .ToList();

        var successCount = 0;

        foreach (var image in images)
        {
            if (await VectorizeProductImageAsync(dbContext, image, cancellationToken))
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <summary>
    /// Vectorizes all product images in the database that don't have embeddings.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="forceRegenerate">If true, regenerates all embeddings even if they exist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple of (successful count, total count).</returns>
    public async Task<(int Success, int Total)> VectorizeAllAsync(
        VisualSearchDbContext dbContext,
        bool forceRegenerate = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProductImages.AsQueryable();

        if (!forceRegenerate)
        {
            query = query.Where(pi => pi.Embedding == null);
        }

        var images = query.ToList();
        var successCount = 0;

        _logger.LogInformation("Starting batch vectorization of {Count} images", images.Count);

        foreach (var image in images)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (await VectorizeProductImageAsync(dbContext, image, cancellationToken))
            {
                successCount++;
            }

            // Small delay to avoid overwhelming external image servers
            await Task.Delay(100, cancellationToken);
        }

        _logger.LogInformation("Batch vectorization completed: {Success}/{Total}", successCount, images.Count);
        return (successCount, images.Count);
    }

    /// <summary>
    /// Detects objects in an image and returns embeddings for each detected furniture item.
    /// Uses batch embedding for optimal performance.
    /// </summary>
    /// <param name="imageBytes">The image bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of detected objects with their embeddings.</returns>
    public async Task<List<DetectedObjectWithEmbedding>> DetectAndEmbedAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DetectedObjectWithEmbedding>();
        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        var stageSw = System.Diagnostics.Stopwatch.StartNew();

        // Decode image
        using var image = Image.Load<Rgb24>(imageBytes);
        var decodeTime = stageSw.ElapsedMilliseconds;
        _logger.LogInformation("[TIMING] ImageDecode: {Time}ms ({Width}x{Height})", decodeTime, image.Width, image.Height);

        // If object detection is available, detect furniture items
        if (_detectionService.IsModelLoaded)
        {
            stageSw.Restart();
            var detections = await _detectionService.DetectObjectsAsync(image, cancellationToken);
            var detectTime = stageSw.ElapsedMilliseconds;
            _logger.LogInformation("[TIMING] YoloDetection: {Time}ms - Detected {Count} objects", detectTime, detections.Count);

            if (detections.Count > 0)
            {
                stageSw.Restart();
                var crops = _detectionService.CropDetections(image, detections);
                var cropTime = stageSw.ElapsedMilliseconds;
                _logger.LogInformation("[TIMING] CropDetections: {Time}ms - {Count} crops", cropTime, crops.Count);

                long embedTime = 0;
                try
                {
                    // Use batch embedding for all crops - single inference call
                    stageSw.Restart();
                    var embeddings = await GenerateBatchEmbeddingsAsync(crops, cancellationToken);
                    embedTime = stageSw.ElapsedMilliseconds;
                    _logger.LogInformation("[TIMING] ClipBatchEmbedding: {Time}ms - {Count} embeddings", embedTime, embeddings.Count);

                    for (int i = 0; i < Math.Min(embeddings.Count, detections.Count); i++)
                    {
                        if (embeddings[i] is not null)
                        {
                            results.Add(new DetectedObjectWithEmbedding
                            {
                                Detection = detections[i],
                                Embedding = embeddings[i]!
                            });
                        }
                    }
                }
                finally
                {
                    // Dispose all crops
                    foreach (var crop in crops)
                    {
                        crop.Dispose();
                    }
                }

                totalSw.Stop();
                _logger.LogInformation("[TIMING] DetectAndEmbed total: {Time}ms | Decode={Decode}ms, Yolo={Yolo}ms, Crop={Crop}ms, Clip={Clip}ms",
                    totalSw.ElapsedMilliseconds, decodeTime, detectTime, cropTime, embedTime);
                return results;
            }
        }

        // Fallback: generate embedding for the full image
        stageSw.Restart();
        var fullImageEmbedding = await GenerateEmbeddingWithFallbackAsync(image, cancellationToken);
        var fallbackTime = stageSw.ElapsedMilliseconds;
        _logger.LogInformation("[TIMING] FallbackEmbedding: {Time}ms", fallbackTime);

        if (fullImageEmbedding is not null)
        {
            results.Add(new DetectedObjectWithEmbedding
            {
                Detection = null, // No specific detection
                Embedding = fullImageEmbedding
            });
        }

        return results;
    }

    /// <summary>
    /// Generates embeddings for multiple images using batch inference.
    /// </summary>
    /// <param name="images">List of images to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of embeddings (may contain nulls for failed images).</returns>
    private async Task<List<float[]?>> GenerateBatchEmbeddingsAsync(
        List<Image<Rgb24>> images,
        CancellationToken cancellationToken)
    {
        if (!_clipService.IsModelLoaded || images.Count == 0)
        {
            // Fall back to individual processing with fallback embeddings
            var fallbackResults = new List<float[]?>();
            foreach (var img in images)
            {
                _logger.LogWarning("CLIP model not loaded, using fallback embedding for batch image");
                fallbackResults.Add(_clipService.GenerateFallbackEmbedding(img));
            }
            return fallbackResults;
        }

        // Use batch inference for optimal performance
        var batchResultsArray = await _clipService.GenerateBatchEmbeddingsAsync(images, cancellationToken);
        var batchResults = batchResultsArray.ToList();

        // Replace nulls with fallback embeddings
        for (int i = 0; i < batchResults.Count; i++)
        {
            if (batchResults[i] is null)
            {
                _logger.LogWarning("Batch embedding returned null for image {Index}, using fallback embedding", i);
                batchResults[i] = _clipService.GenerateFallbackEmbedding(images[i]);
            }
        }

        return batchResults;
    }

    private async Task<float[]?> GenerateEmbeddingWithFallbackAsync(Image<Rgb24> image, CancellationToken cancellationToken)
    {
        if (!_clipService.IsModelLoaded)
        {
            _logger.LogWarning("CLIP model not loaded, using fallback embedding.");
            return _clipService.GenerateFallbackEmbedding(image);
        }

        var embedding = await _clipService.GenerateEmbeddingAsync(image, cancellationToken);
        return embedding ?? _clipService.GenerateFallbackEmbedding(image);
    }
}

/// <summary>
/// Represents a detected object along with its CLIP embedding.
/// </summary>
public sealed class DetectedObjectWithEmbedding
{
    /// <summary>Gets or sets the detection info (null if full image was used).</summary>
    public DetectedObject? Detection { get; set; }

    /// <summary>Gets or sets the CLIP embedding for this object.</summary>
    public required float[] Embedding { get; set; }

    /// <summary>Gets the class name of the detected object.</summary>
    public string ClassName => Detection?.ClassName ?? "Whole Image";

    /// <summary>Gets the bounding box as [x, y, width, height] or null.</summary>
    public float[]? BoundingBox => Detection is not null
        ? [Detection.X1, Detection.Y1, Detection.X2 - Detection.X1, Detection.Y2 - Detection.Y1]
        : null;
}
