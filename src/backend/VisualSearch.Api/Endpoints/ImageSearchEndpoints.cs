using VisualSearch.Api.Application.Services;
using VisualSearch.Api.Contracts.DTOs;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Fast image search endpoint - server does all ML inference.
/// Supports object detection (YOLO) for room images and CLIP embeddings for similarity search.
/// Target: &lt;500ms response time with object detection.
/// </summary>
public static class ImageSearchEndpoints
{
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
        VisualSearchService visualSearchService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
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

            // Delegate to application service
            var result = await visualSearchService.SearchByImageAsync(imageBytes, cancellationToken);

            // Map to response (for backward compatibility with existing API contract)
            return Results.Ok(new ImageSearchResponse
            {
                DetectedObjects = result.DetectedObjects
                    .Select(d => new DetectedObjectResults
                    {
                        ClassName = d.ClassName,
                        BoundingBox = d.BoundingBox,
                        Results = d.Results.Select(MapToProductResult).ToList()
                    })
                    .ToList(),
                Results = result.AllResults.Select(MapToProductResult).ToList(),
                ProcessingTimeMs = result.ProcessingTimeMs,
                UsedObjectDetection = result.UsedObjectDetection,
                ClipModelLoaded = result.ClipModelLoaded,
                YoloModelLoaded = result.YoloModelLoaded
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image search failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    private static ProductResult MapToProductResult(SearchResultDto dto)
    {
        return new ProductResult
        {
            ProductId = dto.ProductId,
            Name = dto.ProductName,
            Price = dto.Price.HasValue ? (float)dto.Price.Value : 0,
            ImageUrl = dto.ImageUrl,
            ProviderName = dto.ProviderName,
            Similarity = (float)dto.Similarity,
            Category = dto.CategoryName,
            ProductUrl = dto.ProductUrl
        };
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
