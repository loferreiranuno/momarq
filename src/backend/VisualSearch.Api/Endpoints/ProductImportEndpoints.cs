using Microsoft.AspNetCore.Mvc;
using VisualSearch.Api.Application.Services;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Endpoints for reviewing and importing extracted products.
/// </summary>
public static class ProductImportEndpoints
{
    /// <summary>
    /// Maps the product import endpoints to the application.
    /// </summary>
    public static void MapProductImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/import")
            .WithTags("ProductImport")
            .RequireAuthorization("Admin");

        // List extracted products for review
        group.MapGet("/", GetExtractedProductsAsync)
            .Produces<ExtractedProductsPagedResult>(200)
            .WithName("GetExtractedProducts")
            .WithDescription("Gets a paginated list of extracted products for review.");

        // Get statistics
        group.MapGet("/stats", GetStatsAsync)
            .Produces<ExtractedProductsStatsDto>(200)
            .WithName("GetExtractedProductStats")
            .WithDescription("Gets statistics about extracted products.");

        // Approve and import a product
        group.MapPost("/{id:long}/approve", ApproveAsync)
            .Produces<ApproveResult>(200)
            .Produces(400)
            .Produces(404)
            .WithName("ApproveExtractedProduct")
            .WithDescription("Approves and imports an extracted product to the products table.");

        // Reject a product
        group.MapPost("/{id:long}/reject", RejectAsync)
            .Produces(200)
            .Produces(400)
            .Produces(404)
            .WithName("RejectExtractedProduct")
            .WithDescription("Rejects an extracted product.");

        // Reset to pending
        group.MapPost("/{id:long}/reset", ResetToPendingAsync)
            .Produces(200)
            .Produces(404)
            .WithName("ResetExtractedProduct")
            .WithDescription("Resets an extracted product back to pending status.");

        // Bulk approve
        group.MapPost("/bulk-approve", BulkApproveAsync)
            .Produces<BulkImportResult>(200)
            .WithName("BulkApproveExtractedProducts")
            .WithDescription("Approves and imports multiple extracted products.");

        // Bulk reject
        group.MapPost("/bulk-reject", BulkRejectAsync)
            .Produces<BulkRejectResult>(200)
            .WithName("BulkRejectExtractedProducts")
            .WithDescription("Rejects multiple extracted products.");
    }

    private static async Task<IResult> GetExtractedProductsAsync(
        ProductImportService importService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ExtractedProductStatus? status = null,
        [FromQuery] int? providerId = null,
        [FromQuery] long? jobId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await importService.GetExtractedProductsAsync(
            page, pageSize, status, providerId, jobId, search, ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetStatsAsync(
        ProductImportService importService,
        CancellationToken ct = default)
    {
        var stats = await importService.GetStatsAsync(ct);
        return Results.Ok(stats);
    }

    private static async Task<IResult> ApproveAsync(
        long id,
        ProductImportService importService,
        HttpContext httpContext,
        [FromBody] ApproveRequest? request = null,
        CancellationToken ct = default)
    {
        var adminUserId = GetAdminUserId(httpContext);
        if (!adminUserId.HasValue)
        {
            return Results.Unauthorized();
        }

        var result = await importService.ApproveAsync(id, adminUserId.Value, request?.CategoryId, ct);

        if (result.IsNotFound)
        {
            return Results.NotFound(new { error = "Extracted product not found" });
        }

        if (result.IsAlreadyProcessed)
        {
            return Results.BadRequest(new { error = $"Product already processed with status: {result.PreviousStatus}" });
        }

        if (result.IsDuplicate)
        {
            return Results.Ok(new ApproveResult
            {
                Success = false,
                IsDuplicate = true,
                ExistingProductId = result.ProductId,
                Message = "Product marked as duplicate - already exists"
            });
        }

        return Results.Ok(new ApproveResult
        {
            Success = true,
            ProductId = result.ProductId,
            Message = "Product successfully imported"
        });
    }

    private static async Task<IResult> RejectAsync(
        long id,
        ProductImportService importService,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        var adminUserId = GetAdminUserId(httpContext);
        if (!adminUserId.HasValue)
        {
            return Results.Unauthorized();
        }

        var success = await importService.RejectAsync(id, adminUserId.Value, ct);

        if (!success)
        {
            return Results.NotFound(new { error = "Extracted product not found or already processed" });
        }

        return Results.Ok(new { message = "Product rejected" });
    }

    private static async Task<IResult> ResetToPendingAsync(
        long id,
        ProductImportService importService,
        CancellationToken ct = default)
    {
        var success = await importService.ResetToPendingAsync(id, ct);

        if (!success)
        {
            return Results.NotFound(new { error = "Extracted product not found or already pending" });
        }

        return Results.Ok(new { message = "Product reset to pending" });
    }

    private static async Task<IResult> BulkApproveAsync(
        ProductImportService importService,
        HttpContext httpContext,
        [FromBody] BulkApproveRequest request,
        CancellationToken ct = default)
    {
        var adminUserId = GetAdminUserId(httpContext);
        if (!adminUserId.HasValue)
        {
            return Results.Unauthorized();
        }

        if (request.Ids == null || request.Ids.Length == 0)
        {
            return Results.BadRequest(new { error = "No product IDs provided" });
        }

        var result = await importService.BulkApproveAsync(
            request.Ids, adminUserId.Value, request.CategoryId, ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> BulkRejectAsync(
        ProductImportService importService,
        HttpContext httpContext,
        [FromBody] BulkRejectRequest request,
        CancellationToken ct = default)
    {
        var adminUserId = GetAdminUserId(httpContext);
        if (!adminUserId.HasValue)
        {
            return Results.Unauthorized();
        }

        if (request.Ids == null || request.Ids.Length == 0)
        {
            return Results.BadRequest(new { error = "No product IDs provided" });
        }

        var count = await importService.BulkRejectAsync(request.Ids, adminUserId.Value, ct);

        return Results.Ok(new BulkRejectResult { RejectedCount = count });
    }

    private static int? GetAdminUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst("userId")?.Value;
        if (int.TryParse(userIdClaim, out var uid))
        {
            return uid;
        }
        return null;
    }
}

/// <summary>
/// Request for approving an extracted product.
/// </summary>
public sealed class ApproveRequest
{
    public int? CategoryId { get; set; }
}

/// <summary>
/// Result of approve operation.
/// </summary>
public sealed class ApproveResult
{
    public bool Success { get; set; }
    public bool IsDuplicate { get; set; }
    public int? ProductId { get; set; }
    public int? ExistingProductId { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Request for bulk approving products.
/// </summary>
public sealed class BulkApproveRequest
{
    public required long[] Ids { get; set; }
    public int? CategoryId { get; set; }
}

/// <summary>
/// Request for bulk rejecting products.
/// </summary>
public sealed class BulkRejectRequest
{
    public required long[] Ids { get; set; }
}

/// <summary>
/// Result of bulk reject operation.
/// </summary>
public sealed class BulkRejectResult
{
    public int RejectedCount { get; set; }
}
