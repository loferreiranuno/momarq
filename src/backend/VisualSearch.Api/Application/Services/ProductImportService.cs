using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Service for importing extracted products to the canonical products table.
/// </summary>
public sealed class ProductImportService
{
    private readonly VisualSearchDbContext _db;
    private readonly ProductService _productService;
    private readonly IProductImageService _productImageService;

    public ProductImportService(
        VisualSearchDbContext db,
        ProductService productService,
        IProductImageService productImageService)
    {
        _db = db;
        _productService = productService;
        _productImageService = productImageService;
    }

    /// <summary>
    /// Gets a paginated list of extracted products for review.
    /// </summary>
    public async Task<ExtractedProductsPagedResult> GetExtractedProductsAsync(
        int page,
        int pageSize,
        ExtractedProductStatus? status = null,
        int? providerId = null,
        long? jobId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.CrawlExtractedProducts
            .Include(e => e.Provider)
            .Include(e => e.CrawlJob)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (providerId.HasValue)
        {
            query = query.Where(e => e.ProviderId == providerId.Value);
        }

        if (jobId.HasValue)
        {
            query = query.Where(e => e.CrawlJobId == jobId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(e =>
                (e.Name != null && e.Name.ToLower().Contains(searchLower)) ||
                (e.ExternalId != null && e.ExternalId.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var rawItems = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.CrawlJobId,
                e.ProviderId,
                ProviderName = e.Provider!.Name,
                e.ExternalId,
                e.Name,
                e.Description,
                e.Price,
                e.Currency,
                e.ProductUrl,
                e.ImageUrlsJson,
                e.Status,
                e.ImportedProductId,
                e.ReviewedAt,
                e.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rawItems.Select(e => new ExtractedProductDto
        {
            Id = e.Id,
            CrawlJobId = e.CrawlJobId,
            ProviderId = e.ProviderId,
            ProviderName = e.ProviderName,
            ExternalId = e.ExternalId,
            Name = e.Name,
            Description = e.Description,
            Price = e.Price,
            Currency = e.Currency,
            ProductUrl = e.ProductUrl,
            ImageUrls = e.ImageUrlsJson != null
                ? JsonSerializer.Deserialize<List<string>>(e.ImageUrlsJson) ?? new List<string>()
                : new List<string>(),
            Status = e.Status,
            ImportedProductId = e.ImportedProductId,
            ReviewedAt = e.ReviewedAt,
            CreatedAt = e.CreatedAt
        }).ToList();

        return new ExtractedProductsPagedResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    /// <summary>
    /// Gets statistics about extracted products.
    /// </summary>
    public async Task<ExtractedProductsStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _db.CrawlExtractedProducts
            .GroupBy(_ => 1)
            .Select(g => new ExtractedProductsStatsDto
            {
                TotalCount = g.Count(),
                PendingCount = g.Count(e => e.Status == ExtractedProductStatus.Pending),
                ApprovedCount = g.Count(e => e.Status == ExtractedProductStatus.Approved),
                RejectedCount = g.Count(e => e.Status == ExtractedProductStatus.Rejected),
                DuplicateCount = g.Count(e => e.Status == ExtractedProductStatus.Duplicate)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new ExtractedProductsStatsDto();
    }

    /// <summary>
    /// Approves and imports an extracted product to the products table.
    /// </summary>
    public async Task<ImportResult> ApproveAsync(
        long extractedProductId,
        int adminUserId,
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var extracted = await _db.CrawlExtractedProducts
            .Include(e => e.Provider)
            .FirstOrDefaultAsync(e => e.Id == extractedProductId, cancellationToken);

        if (extracted == null)
        {
            return ImportResult.NotFound();
        }

        if (extracted.Status != ExtractedProductStatus.Pending)
        {
            return ImportResult.AlreadyProcessed(extracted.Status);
        }

        // Check for existing product with same provider + external ID
        if (!string.IsNullOrWhiteSpace(extracted.ExternalId))
        {
            var existingProduct = await _db.Products
                .FirstOrDefaultAsync(p =>
                    p.ProviderId == extracted.ProviderId &&
                    p.ExternalId == extracted.ExternalId, cancellationToken);

            if (existingProduct != null)
            {
                // Mark as duplicate
                extracted.Status = ExtractedProductStatus.Duplicate;
                extracted.ImportedProductId = existingProduct.Id;
                extracted.ReviewedAt = DateTime.UtcNow;
                extracted.ReviewedByAdminUserId = adminUserId;
                await _db.SaveChangesAsync(cancellationToken);

                return ImportResult.Duplicate(existingProduct.Id);
            }
        }

        // Create the product
        var productId = await _productService.CreateAdminProductAsync(
            providerId: extracted.ProviderId,
            name: extracted.Name ?? "Unknown Product",
            externalId: extracted.ExternalId,
            description: extracted.Description,
            price: extracted.Price ?? 0,
            currency: extracted.Currency,
            categoryId: categoryId,
            productUrl: extracted.ProductUrl,
            cancellationToken: cancellationToken);

        // Import images
        if (!string.IsNullOrEmpty(extracted.ImageUrlsJson))
        {
            var imageUrls = JsonSerializer.Deserialize<List<string>>(extracted.ImageUrlsJson);
            if (imageUrls?.Count > 0)
            {
                for (int i = 0; i < imageUrls.Count; i++)
                {
                    var isPrimary = i == 0;
                    await _productImageService.AddFromUrlAsync(
                        productId, imageUrls[i], isPrimary, cancellationToken);
                }
            }
        }

        // Update extracted product status
        extracted.Status = ExtractedProductStatus.Approved;
        extracted.ImportedProductId = productId;
        extracted.ReviewedAt = DateTime.UtcNow;
        extracted.ReviewedByAdminUserId = adminUserId;
        await _db.SaveChangesAsync(cancellationToken);

        return ImportResult.Success(productId);
    }

    /// <summary>
    /// Rejects an extracted product.
    /// </summary>
    public async Task<bool> RejectAsync(
        long extractedProductId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var extracted = await _db.CrawlExtractedProducts
            .FirstOrDefaultAsync(e => e.Id == extractedProductId, cancellationToken);

        if (extracted == null)
        {
            return false;
        }

        if (extracted.Status != ExtractedProductStatus.Pending)
        {
            return false;
        }

        extracted.Status = ExtractedProductStatus.Rejected;
        extracted.ReviewedAt = DateTime.UtcNow;
        extracted.ReviewedByAdminUserId = adminUserId;
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Bulk approves multiple extracted products.
    /// </summary>
    public async Task<BulkImportResult> BulkApproveAsync(
        IEnumerable<long> extractedProductIds,
        int adminUserId,
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new BulkImportResult();

        foreach (var id in extractedProductIds)
        {
            try
            {
                var importResult = await ApproveAsync(id, adminUserId, categoryId, cancellationToken);
                if (importResult.IsSuccess)
                {
                    result.SuccessCount++;
                    result.ImportedProductIds.Add(importResult.ProductId!.Value);
                }
                else if (importResult.IsDuplicate)
                {
                    result.DuplicateCount++;
                }
                else
                {
                    result.FailedCount++;
                }
            }
            catch
            {
                result.FailedCount++;
            }
        }

        return result;
    }

    /// <summary>
    /// Bulk rejects multiple extracted products.
    /// </summary>
    public async Task<int> BulkRejectAsync(
        IEnumerable<long> extractedProductIds,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var id in extractedProductIds)
        {
            if (await RejectAsync(id, adminUserId, cancellationToken))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Resets an extracted product back to pending status.
    /// </summary>
    public async Task<bool> ResetToPendingAsync(
        long extractedProductId,
        CancellationToken cancellationToken = default)
    {
        var extracted = await _db.CrawlExtractedProducts
            .FirstOrDefaultAsync(e => e.Id == extractedProductId, cancellationToken);

        if (extracted == null || extracted.Status == ExtractedProductStatus.Pending)
        {
            return false;
        }

        extracted.Status = ExtractedProductStatus.Pending;
        extracted.ImportedProductId = null;
        extracted.ReviewedAt = null;
        extracted.ReviewedByAdminUserId = null;
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}

/// <summary>
/// Result of an import operation.
/// </summary>
public sealed class ImportResult
{
    public bool IsSuccess { get; private set; }
    public bool IsNotFound { get; private set; }
    public bool IsAlreadyProcessed { get; private set; }
    public bool IsDuplicate { get; private set; }
    public int? ProductId { get; private set; }
    public ExtractedProductStatus? PreviousStatus { get; private set; }

    public static ImportResult Success(int productId) => new() { IsSuccess = true, ProductId = productId };
    public static ImportResult NotFound() => new() { IsNotFound = true };
    public static ImportResult AlreadyProcessed(ExtractedProductStatus status) =>
        new() { IsAlreadyProcessed = true, PreviousStatus = status };
    public static ImportResult Duplicate(int existingProductId) =>
        new() { IsDuplicate = true, ProductId = existingProductId };
}

/// <summary>
/// Result of a bulk import operation.
/// </summary>
public sealed class BulkImportResult
{
    public int SuccessCount { get; set; }
    public int DuplicateCount { get; set; }
    public int FailedCount { get; set; }
    public List<int> ImportedProductIds { get; set; } = [];
}

/// <summary>
/// DTO for extracted product display.
/// </summary>
public sealed class ExtractedProductDto
{
    public long Id { get; set; }
    public long CrawlJobId { get; set; }
    public int ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public string? ExternalId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? ProductUrl { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public ExtractedProductStatus Status { get; set; }
    public int? ImportedProductId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Paged result for extracted products.
/// </summary>
public sealed class ExtractedProductsPagedResult
{
    public List<ExtractedProductDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Statistics for extracted products.
/// </summary>
public sealed class ExtractedProductsStatsDto
{
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int DuplicateCount { get; set; }
}
