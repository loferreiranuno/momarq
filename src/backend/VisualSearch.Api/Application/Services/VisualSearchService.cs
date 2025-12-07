using Pgvector;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for visual search operations.
/// Handles object detection, embedding generation, and similarity search.
/// </summary>
public sealed class VisualSearchService
{
    private readonly IProductImageRepository _productImageRepository;
    private readonly VectorizationService _vectorizationService;
    private readonly ClipEmbeddingService _clipEmbeddingService;
    private readonly ObjectDetectionService _objectDetectionService;
    private readonly ILogger<VisualSearchService> _logger;

    private const int MaxResultsPerObject = 8;
    private const float MinSimilarityThreshold = 0.80f;

    public VisualSearchService(
        IProductImageRepository productImageRepository,
        VectorizationService vectorizationService,
        ClipEmbeddingService clipEmbeddingService,
        ObjectDetectionService objectDetectionService,
        ILogger<VisualSearchService> logger)
    {
        _productImageRepository = productImageRepository;
        _vectorizationService = vectorizationService;
        _clipEmbeddingService = clipEmbeddingService;
        _objectDetectionService = objectDetectionService;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether CLIP model is loaded and available.
    /// </summary>
    public bool IsClipModelLoaded => _clipEmbeddingService.IsModelLoaded;

    /// <summary>
    /// Gets whether YOLO model is loaded and available.
    /// </summary>
    public bool IsYoloModelLoaded => _objectDetectionService.IsModelLoaded;

    /// <summary>
    /// Gets whether object detection is available.
    /// </summary>
    public bool IsDetectionAvailable => _vectorizationService.IsDetectionAvailable;

    /// <summary>
    /// Performs visual search on an uploaded image.
    /// Detects objects if possible, then searches for similar products.
    /// Returns unique products (no duplicates from multiple images of same product).
    /// </summary>
    public async Task<VisualSearchResultDto> SearchByImageAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var allResults = new List<DetectedObjectResultDto>();
        var usedObjectDetection = false;

        // Try object detection first (for room images)
        if (_vectorizationService.IsDetectionAvailable)
        {
            var detectedEmbeddings = await _vectorizationService.DetectAndEmbedAsync(imageBytes, cancellationToken);

            if (detectedEmbeddings.Count > 0)
            {
                usedObjectDetection = true;
                _logger.LogInformation("Detected {Count} furniture objects in image", detectedEmbeddings.Count);

                foreach (var detectedObj in detectedEmbeddings)
                {
                    var queryVector = new Vector(detectedObj.Embedding);
                    var searchResults = await SearchByVectorWithDeduplicationAsync(queryVector, MaxResultsPerObject, cancellationToken);

                    if (searchResults.Count > 0)
                    {
                        allResults.Add(new DetectedObjectResultDto(
                            ClassName: detectedObj.ClassName,
                            BoundingBox: detectedObj.BoundingBox,
                            Results: searchResults
                        ));
                    }
                }
            }
        }

        // Fallback: no objects detected or detection not available - use whole image
        if (!usedObjectDetection || allResults.Count == 0)
        {
            float[]? embedding = null;

            if (_clipEmbeddingService.IsModelLoaded)
            {
                embedding = await _clipEmbeddingService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
            }

            embedding ??= await _clipEmbeddingService.GenerateFallbackEmbeddingAsync(imageBytes, cancellationToken);

            var queryVector = new Vector(embedding);
            var searchResults = await SearchByVectorWithDeduplicationAsync(queryVector, MaxResultsPerObject, cancellationToken);

            allResults.Add(new DetectedObjectResultDto(
                ClassName: "Whole Image",
                BoundingBox: null,
                Results: searchResults
            ));
        }

        sw.Stop();

        _logger.LogInformation(
            "Search completed in {Time}ms, found {ObjectCount} objects with {ResultCount} unique products",
            sw.ElapsedMilliseconds,
            allResults.Count,
            allResults.Sum(r => r.Results.Count));

        return new VisualSearchResultDto(
            DetectedObjects: allResults,
            ProcessingTimeMs: (int)sw.ElapsedMilliseconds,
            UsedObjectDetection: usedObjectDetection,
            ClipModelLoaded: _clipEmbeddingService.IsModelLoaded,
            YoloModelLoaded: _objectDetectionService.IsModelLoaded
        );
    }

    /// <summary>
    /// Searches for similar products by vector embedding.
    /// Returns unique products (deduplicates multiple images of same product).
    /// </summary>
    private async Task<List<SearchResultDto>> SearchByVectorWithDeduplicationAsync(
        Vector queryVector,
        int limit,
        CancellationToken cancellationToken)
    {
        // Get more results than needed to allow for deduplication
        var searchLimit = limit * 3;

        var imageResults = await _productImageRepository.SearchByVectorAsync(
            queryVector,
            searchLimit,
            cancellationToken);

        // KEY FIX: Deduplicate by ProductId - keep only the best match per product
        // This prevents showing the same product multiple times when it has multiple images
        var uniqueProducts = imageResults
            .Where(r => (1 - r.Distance) >= MinSimilarityThreshold)
            .GroupBy(r => r.Image.ProductId)
            .Select(g => g.OrderBy(r => r.Distance).First()) // Keep best match per product
            .OrderBy(r => r.Distance)
            .Take(limit)
            .Select(r => new SearchResultDto(
                ProductId: r.Image.ProductId,
                ProductName: r.Image.Product?.Name ?? "Unknown",
                Price: r.Image.Product?.Price,
                Currency: r.Image.Product?.Currency,
                ProviderName: r.Image.Product?.Provider?.Name ?? "Unknown",
                CategoryName: r.Image.Product?.Category?.Name,
                ImageUrl: r.Image.ImageUrl,
                ProductUrl: r.Image.Product?.ProductUrl,
                Similarity: 1 - r.Distance
            ))
            .ToList();

        return uniqueProducts;
    }
}

/// <summary>
/// Result from visual search operation.
/// </summary>
public record VisualSearchResultDto(
    List<DetectedObjectResultDto> DetectedObjects,
    int ProcessingTimeMs,
    bool UsedObjectDetection,
    bool ClipModelLoaded,
    bool YoloModelLoaded
)
{
    /// <summary>
    /// Flat list of all results for backward compatibility.
    /// </summary>
    public List<SearchResultDto> AllResults => DetectedObjects.SelectMany(d => d.Results).ToList();
}

/// <summary>
/// Results grouped by detected object.
/// </summary>
public record DetectedObjectResultDto(
    string ClassName,
    float[]? BoundingBox,
    List<SearchResultDto> Results
);
