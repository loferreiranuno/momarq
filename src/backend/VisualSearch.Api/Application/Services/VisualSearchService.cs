using Microsoft.Extensions.Options;
using Pgvector;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for visual search operations.
/// Handles object detection, embedding generation, and similarity search with bulk operations.
/// </summary>
public sealed class VisualSearchService
{
    private readonly IProductImageRepository _productImageRepository;
    private readonly VectorizationService _vectorizationService;
    private readonly ClipEmbeddingService _clipEmbeddingService;
    private readonly ObjectDetectionService _objectDetectionService;
    private readonly ILogger<VisualSearchService> _logger;
    private readonly SearchSettings _searchSettings;

    public VisualSearchService(
        IProductImageRepository productImageRepository,
        VectorizationService vectorizationService,
        ClipEmbeddingService clipEmbeddingService,
        ObjectDetectionService objectDetectionService,
        IOptions<ModelSettings> settings,
        ILogger<VisualSearchService> logger)
    {
        _productImageRepository = productImageRepository;
        _vectorizationService = vectorizationService;
        _clipEmbeddingService = clipEmbeddingService;
        _objectDetectionService = objectDetectionService;
        _searchSettings = settings.Value.Search;
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
    /// Detects objects if possible, then searches for similar products using bulk operations.
    /// Returns unique products (no duplicates across all detected objects).
    /// </summary>
    public async Task<VisualSearchResultDto> SearchByImageAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        var stageSw = System.Diagnostics.Stopwatch.StartNew();
        var timings = new Dictionary<string, long>();
        
        var allResults = new List<DetectedObjectResultDto>();
        var usedObjectDetection = false;

        // Try object detection first (for room images)
        if (_vectorizationService.IsDetectionAvailable)
        {
            stageSw.Restart();
            var detectedEmbeddings = await _vectorizationService.DetectAndEmbedAsync(imageBytes, cancellationToken);
            timings["DetectAndEmbed"] = stageSw.ElapsedMilliseconds;

            if (detectedEmbeddings.Count > 0)
            {
                usedObjectDetection = true;
                _logger.LogInformation("[TIMING] DetectAndEmbed: {Time}ms - Detected {Count} objects", 
                    timings["DetectAndEmbed"], detectedEmbeddings.Count);

                // Use bulk search - results are deduplicated by ProductId
                stageSw.Restart();
                var embeddings = detectedEmbeddings.Select(d => new Vector(d.Embedding)).ToList();
                var bulkResults = await _productImageRepository.SearchByVectorsAsync(
                    embeddings,
                    limitPerEmbedding: _searchSettings.MaxResultsPerObject,
                    totalLimit: _searchSettings.MaxTotalResults,
                    cancellationToken);
                timings["DatabaseSearch"] = stageSw.ElapsedMilliseconds;
                _logger.LogInformation("[TIMING] DatabaseSearch: {Time}ms - Found {Count} results", 
                    timings["DatabaseSearch"], bulkResults.Count);

                // Post-processing: filter and distribute results to detected objects
                stageSw.Restart();
                
                // Filter by similarity threshold - bulkResults are already deduplicated by ProductId
                var filteredResults = bulkResults
                    .Where(r => (1 - r.Distance) >= _searchSettings.MinSimilarityThreshold)
                    .OrderBy(r => r.Distance)
                    .ToList();

                // Global set of used ProductIds to prevent duplicates across all detected objects
                var usedProductIds = new HashSet<int>();

                // Distribute results to detected objects - use DB-calculated distances directly
                // Each object gets results in order of best match (already sorted by distance)
                foreach (var detectedObj in detectedEmbeddings)
                {
                    var objectResults = new List<SearchResultDto>();

                    // Take best available matches not yet assigned to another object
                    var matchesForObject = filteredResults
                        .Where(r => !usedProductIds.Contains(r.ProductId))
                        .Take(_searchSettings.MaxResultsPerObject)
                        .ToList();

                    foreach (var match in matchesForObject)
                    {
                        usedProductIds.Add(match.ProductId);
                        objectResults.Add(match.ToSearchResultDto());
                    }

                    if (objectResults.Count > 0)
                    {
                        allResults.Add(new DetectedObjectResultDto(
                            ClassName: detectedObj.ClassName,
                            BoundingBox: detectedObj.BoundingBox,
                            Results: objectResults
                        ));
                    }
                }
                
                timings["PostProcessing"] = stageSw.ElapsedMilliseconds;
                _logger.LogInformation("[TIMING] PostProcessing: {Time}ms", timings["PostProcessing"]);
            }
        }

        // Fallback: no objects detected or detection not available - use whole image
        if (!usedObjectDetection || allResults.Count == 0)
        {
            stageSw.Restart();
            float[]? embedding = null;

            if (_clipEmbeddingService.IsModelLoaded)
            {
                embedding = await _clipEmbeddingService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
            }

            embedding ??= await _clipEmbeddingService.GenerateFallbackEmbeddingAsync(imageBytes, cancellationToken);
            timings["FallbackEmbedding"] = stageSw.ElapsedMilliseconds;

            stageSw.Restart();
            var queryVector = new Vector(embedding);
            var searchResults = await SearchByVectorWithDeduplicationAsync(queryVector, _searchSettings.MaxResultsPerObject, cancellationToken);
            timings["FallbackSearch"] = stageSw.ElapsedMilliseconds;

            allResults.Add(new DetectedObjectResultDto(
                ClassName: "Whole Image",
                BoundingBox: null,
                Results: searchResults
            ));
        }

        totalSw.Stop();

        // Log comprehensive timing breakdown
        var timingsSummary = string.Join(", ", timings.Select(t => $"{t.Key}={t.Value}ms"));
        _logger.LogInformation(
            "[TIMING] Search completed in {Time}ms | {Timings} | Objects={ObjectCount}, Results={ResultCount}",
            totalSw.ElapsedMilliseconds,
            timingsSummary,
            allResults.Count,
            allResults.Sum(r => r.Results.Count));

        return new VisualSearchResultDto(
            DetectedObjects: allResults,
            ProcessingTimeMs: (int)totalSw.ElapsedMilliseconds,
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

        // Deduplicate by ProductId - keep only the best match per product
        // Results already use projection DTO, so just filter and convert
        var uniqueProducts = imageResults
            .Where(r => (1 - r.Distance) >= _searchSettings.MinSimilarityThreshold)
            .GroupBy(r => r.ProductId)
            .Select(g => g.OrderBy(r => r.Distance).First())
            .OrderBy(r => r.Distance)
            .Take(limit)
            .Select(r => r.ToSearchResultDto())
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
