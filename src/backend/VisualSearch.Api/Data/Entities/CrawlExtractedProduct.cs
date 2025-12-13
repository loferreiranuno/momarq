using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents a product extraction result from a crawled page.
/// This is an audit record and is intentionally separate from the canonical Product aggregate.
/// </summary>
public class CrawlExtractedProduct
{
    /// <summary>
    /// Gets or sets the unique identifier for the extracted product record.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the crawl job foreign key.
    /// </summary>
    public long CrawlJobId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the crawl job.
    /// </summary>
    public CrawlJob? CrawlJob { get; set; }

    /// <summary>
    /// Gets or sets the crawl page foreign key.
    /// </summary>
    public long CrawlPageId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the crawl page.
    /// </summary>
    public CrawlPage? CrawlPage { get; set; }

    /// <summary>
    /// Gets or sets the provider foreign key.
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the provider.
    /// </summary>
    public Provider? Provider { get; set; }

    /// <summary>
    /// Gets or sets the provider external identifier (SKU/article number) when available.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the extracted name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the extracted description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the extracted price when available.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Gets or sets the extracted currency code.
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Gets or sets the extracted product URL.
    /// </summary>
    public string? ProductUrl { get; set; }

    /// <summary>
    /// Gets or sets the extracted image URLs as JSON.
    /// </summary>
    public string? ImageUrlsJson { get; set; }

    /// <summary>
    /// Gets or sets the raw extraction payload as JSON for auditing.
    /// </summary>
    public string? RawJson { get; set; }

    /// <summary>
    /// Gets or sets the review/import status of this extracted product.
    /// </summary>
    public ExtractedProductStatus Status { get; set; } = ExtractedProductStatus.Pending;

    /// <summary>
    /// Gets or sets the ID of the imported product if approved.
    /// </summary>
    public int? ImportedProductId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the imported product.
    /// </summary>
    public Product? ImportedProduct { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the product was reviewed (UTC).
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Gets or sets the admin user who reviewed this product.
    /// </summary>
    public int? ReviewedByAdminUserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the reviewing admin user.
    /// </summary>
    public AdminUser? ReviewedByAdminUser { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the extracted product was recorded (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
