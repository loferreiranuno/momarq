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
/// Target: &lt;200ms response time.
/// </summary>
public static class ImageSearchEndpoints
{
    private const int MaxResultsPerObject = 8;

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
        ClipEmbeddingService clipService,
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

            // Generate embedding - use CLIP if available, otherwise fallback
            float[] embedding;
            if (clipService.IsModelLoaded)
            {
                var clipEmbedding = await clipService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
                embedding = clipEmbedding ?? await clipService.GenerateFallbackEmbeddingAsync(imageBytes, cancellationToken);
            }
            else
            {
                embedding = await clipService.GenerateFallbackEmbeddingAsync(imageBytes, cancellationToken);
            }

            var embeddingTime = sw.ElapsedMilliseconds;
            logger.LogInformation("CLIP embedding generated in {Time}ms", embeddingTime);

            // Search for similar products using pgvector
            var queryVector = new Vector(embedding);

            var results = await dbContext.ProductImages
                .Include(pi => pi.Product)
                    .ThenInclude(p => p!.Provider)
                .OrderBy(pi => pi.Embedding!.CosineDistance(queryVector))
                .Take(MaxResultsPerObject)
                .Select(pi => new ProductResult
                {
                    ProductId = pi.ProductId,
                    Name = pi.Product!.Name,
                    Price = (float)pi.Product.Price,
                    ImageUrl = pi.ImageUrl,
                    ProviderName = pi.Product.Provider!.Name,
                    Similarity = (float)(1 - pi.Embedding!.CosineDistance(queryVector))
                })
                .ToListAsync(cancellationToken);

            sw.Stop();
            logger.LogInformation("Search completed in {Time}ms total", sw.ElapsedMilliseconds);

            return Results.Ok(new ImageSearchResponse
            {
                Results = results,
                ProcessingTimeMs = (int)sw.ElapsedMilliseconds,
                EmbeddingTimeMs = (int)embeddingTime
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image search failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }
}

public class ImageSearchResponse
{
    public List<ProductResult> Results { get; set; } = [];
    public int ProcessingTimeMs { get; set; }
    public int EmbeddingTimeMs { get; set; }
}

public class ProductResult
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public float Price { get; set; }
    public string ImageUrl { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public float Similarity { get; set; }
}
