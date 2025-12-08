using Microsoft.AspNetCore.Mvc;
using VisualSearch.Api.Application.Services;
using VisualSearch.Api.Contracts.DTOs;

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
            .Produces<IEnumerable<CategorySummaryDto>>()
            .WithName("GetPublicCategories")
            .WithDescription("Gets all categories. Optionally filter by detection enabled status.");
    }

    /// <summary>
    /// Gets all categories with optional filtering.
    /// </summary>
    private static async Task<IResult> GetCategoriesAsync(
        CategoryService categoryService,
        [FromQuery] bool? detectionEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var categories = await categoryService.GetCategoriesAsync(detectionEnabled, cancellationToken);
        return Results.Ok(categories);
    }
}
