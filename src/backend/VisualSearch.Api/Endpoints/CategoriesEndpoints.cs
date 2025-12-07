using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Data;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Public endpoints for retrieving category data.
/// These endpoints do not require authentication.
/// </summary>
public static class CategoriesEndpoints
{
    /// <summary>
    /// Maps the public categories endpoints to the application.
    /// </summary>
    public static void MapCategoriesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories");

        group.MapGet("/", GetCategoriesAsync)
            .WithName("GetPublicCategories")
            .WithDescription("Gets all categories. Optionally filter by detection enabled status.");
    }

    /// <summary>
    /// Gets all categories with optional filtering.
    /// </summary>
    private static async Task<IResult> GetCategoriesAsync(
        VisualSearchDbContext dbContext,
        [Microsoft.AspNetCore.Mvc.FromQuery] bool? detectionEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Categories.AsQueryable();

        if (detectionEnabled.HasValue)
        {
            query = query.Where(c => c.DetectionEnabled == detectionEnabled.Value);
        }

        var categories = await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                CocoClassId = c.CocoClassId,
                DetectionEnabled = c.DetectionEnabled
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(categories);
    }

    /// <summary>
    /// Response DTO for category data.
    /// </summary>
    private sealed class CategoryResponse
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int CocoClassId { get; set; }
        public bool DetectionEnabled { get; set; }
    }
}
