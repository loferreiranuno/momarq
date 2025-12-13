using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents a single crawled (or attempted) page for a crawl job.
/// Stores audit information and optionally the downloaded content.
/// </summary>
public class CrawlPage
{
    /// <summary>
    /// Gets or sets the unique identifier for the crawl page.
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
    /// Gets or sets the page URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the crawl status for this page.
    /// </summary>
    public CrawlPageStatus Status { get; set; } = CrawlPageStatus.Queued;

    /// <summary>
    /// Gets or sets the HTTP status code returned when fetching the page.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Gets or sets the response content type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the extracted HTML title when available.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the stored content when available.
    /// </summary>
    public string? ContentSha256 { get; set; }

    /// <summary>
    /// Gets or sets the raw content stored for auditing (typically HTML).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets an error message when page processing fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the page was discovered/queued (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when the page was fetched (UTC).
    /// </summary>
    public DateTime? FetchedAt { get; set; }

    /// <summary>
    /// Gets or sets the extracted products found on this page.
    /// </summary>
    public ICollection<CrawlExtractedProduct> ExtractedProducts { get; set; } = [];
}
