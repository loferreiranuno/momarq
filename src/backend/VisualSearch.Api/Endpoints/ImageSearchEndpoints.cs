using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using VisualSearch.Api.Data;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Fast image search endpoint - server does all ML inference.
/// Supports object detection (YOLO) for room images and CLIP embeddings for similarity search.
/// Target: &lt;500ms response time with object detection.
/// </summary>
public static class ImageSearchEndpoints
{
    private const int MaxResultsPerObject = 8;
    private const float MinSimilarityThreshold = 0.30f; // 30% minimum similarity

    public static void MapImageSearchEndpoints(this WebApplication app)
    {
        app.MapPost("/api/search/image", HandleImageSearchAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImageSearchResponse>(200)
            .WithName("ImageSearch")
            .WithDescription("Upload image for visual search. Server performs detection and embedding.")
            .WithTags("Search")
            .DisableAntiforgery();
    }

    private static async Task<IResult> HandleImageSearchAsync(
        HttpRequest request,
        VisualSearchDbContext dbContext,
        VectorizationService vectorizationService,
        ClipEmbeddingService clipService,
        ObjectDetectionService detectionService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Read image from request
            byte[] imageBytes;
            
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(cancellationToken);
                if (form.Files.Count > 0)
                {
                    var file = form.Files[0];
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms, cancellationToken);
                    imageBytes = ms.ToArray();
                }
                else
                {
                    return Results.BadRequest(new { error = "No image file in form" });
                }
            }
            else
            {
                // Raw binary body
                using var ms = new MemoryStream();
                await request.Body.CopyToAsync(ms, cancellationToken);
                imageBytes = ms.ToArray();
            }

            if (imageBytes.Length == 0)
            {
                return Results.BadRequest(new { error = "No image provided" });
            }

            logger.LogInformation("Received image: {Size} bytes", imageBytes.Length);

            // Try object detection first (for room images)
            var allResults = new List<DetectedObjectResults>();
            var usedObjectDetection = false;

            if (vectorizationService.IsDetectionAvailable)
            {
                var detectedEmbeddings = await vectorizationService.DetectAndEmbedAsync(imageBytes, cancellationToken);
                
                if (detectedEmbeddings.Count > 0)
                {
                    usedObjectDetection = true;
                    logger.LogInformation("Detected {Count} furniture objects in image", detectedEmbeddings.Count);

                    // Search for each detected object
                    foreach (var detectedObj in detectedEmbeddings)
                    {
                        var queryVector = new Vector(detectedObj.Embedding);
                        
                        var objectResults = await dbContext.ProductImages
                            .Where(pi => pi.Embedding != null)
                            .Include(pi => pi.Product)
                                .ThenInclude(p => p!.Provider)
                            .Select(pi => new
                            {
                                Image = pi,
                                Distance = pi.Embedding!.CosineDistance(queryVector)
                            })
                            .Where(x => (1 - x.Distance) >= MinSimilarityThreshold)
                            .OrderBy(x => x.Distance)
                            .Take(MaxResultsPerObject)
                            .Select(x => new ProductResult
                            {
                                ProductId = x.Image.ProductId,
                                Name = x.Image.Product!.Name,
                                Price = (float)x.Image.Product.Price,
                                ImageUrl = x.Image.ImageUrl,
                                ProviderName = x.Image.Product.Provider!.Name,
                                Similarity = (float)(1 - x.Distance),
                                Category = x.Image.Product.Category,
                                ProductUrl = x.Image.Product.ProductUrl
                            })
                            .ToListAsync(cancellationToken);

                        if (objectResults.Count > 0)
                        {
                            allResults.Add(new DetectedObjectResults
                            {
                                ClassName = detectedObj.ClassName,
                                BoundingBox = detectedObj.BoundingBox,
                                Results = objectResults
                            });
                        }
                    }
                }
            }

            // Fallback: no objects detected or object detection not available - use whole image
            if (!usedObjectDetection || allResults.Count == 0)
            {
                float[]? embedding = null;
                
                if (clipService.IsModelLoaded)
                {
                    embedding = await clipService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
                }
                
                // Use fallback if CLIP failed
                embedding ??= await clipService.GenerateFallbackEmbeddingAsync(imageBytes, cancellationToken);

                var queryVector = new Vector(embedding);

                var results = await dbContext.ProductImages
                    .Where(pi => pi.Embedding != null)
                    .Include(pi => pi.Product)
                        .ThenInclude(p => p!.Provider)
                    .Select(pi => new
                    {
                        Image = pi,
                        Distance = pi.Embedding!.CosineDistance(queryVector)
                    })
                    .Where(x => (1 - x.Distance) >= MinSimilarityThreshold)
                    .OrderBy(x => x.Distance)
                    .Take(MaxResultsPerObject)
                    .Select(x => new ProductResult
                    {
                        ProductId = x.Image.ProductId,
                        Name = x.Image.Product!.Name,
                        Price = (float)x.Image.Product.Price,
                        ImageUrl = x.Image.ImageUrl,
                        ProviderName = x.Image.Product.Provider!.Name,
                        Similarity = (float)(1 - x.Distance),
                        Category = x.Image.Product.Category,
                        ProductUrl = x.Image.Product.ProductUrl
                    })
                    .ToListAsync(cancellationToken);

                allResults.Add(new DetectedObjectResults
                {
                    ClassName = "Whole Image",
                    BoundingBox = null,
                    Results = results
                });
            }

            sw.Stop();
            logger.LogInformation("Search completed in {Time}ms total, found {ObjectCount} objects with {ResultCount} total results", 
                sw.ElapsedMilliseconds, 
                allResults.Count,
                allResults.Sum(r => r.Results.Count));

            return Results.Ok(new ImageSearchResponse
            {
                DetectedObjects = allResults,
                Results = allResults.SelectMany(r => r.Results).ToList(), // Flat list for backward compatibility
                ProcessingTimeMs = (int)sw.ElapsedMilliseconds,
                UsedObjectDetection = usedObjectDetection,
                ClipModelLoaded = clipService.IsModelLoaded,
                YoloModelLoaded = detectionService.IsModelLoaded
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image search failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }
}

/// <summary>
/// Response from image search endpoint.
/// </summary>
public class ImageSearchResponse
{
    /// <summary>
    /// Results grouped by detected objects (for room images).
    /// </summary>
    public List<DetectedObjectResults> DetectedObjects { get; set; } = [];
    
    /// <summary>
    /// Flat list of all results for backward compatibility.
    /// </summary>
    public List<ProductResult> Results { get; set; } = [];
    
    /// <summary>
    /// Total processing time in milliseconds.
    /// </summary>
    public int ProcessingTimeMs { get; set; }
    
    /// <summary>
    /// Whether object detection (YOLO) was used.
    /// </summary>
    public bool UsedObjectDetection { get; set; }
    
    /// <summary>
    /// Whether CLIP model is loaded.
    /// </summary>
    public bool ClipModelLoaded { get; set; }
    
    /// <summary>
    /// Whether YOLO model is loaded.
    /// </summary>
    public bool YoloModelLoaded { get; set; }
}

/// <summary>
/// Results for a detected object in the image.
/// </summary>
public class DetectedObjectResults
{
    /// <summary>
    /// Class name of detected object (e.g., "chair", "couch").
    /// </summary>
    public string ClassName { get; set; } = "";
    
    /// <summary>
    /// Bounding box coordinates [x, y, width, height].
    /// </summary>
    public float[]? BoundingBox { get; set; }
    
    /// <summary>
    /// Similar products for this detected object.
    /// </summary>
    public List<ProductResult> Results { get; set; } = [];
}

/// <summary>
/// A product result from visual search.
/// </summary>
public class ProductResult
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public float Price { get; set; }
    public string ImageUrl { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public float Similarity { get; set; }
    public string? Category { get; set; }
    public string? ProductUrl { get; set; }
}
