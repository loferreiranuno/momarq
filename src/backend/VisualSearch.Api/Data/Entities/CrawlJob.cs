using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents a crawl job requested by an admin for a specific provider.
/// This is an audit/control-plane record used by the API and crawler worker.
/// </summary>
public class CrawlJob
{
    /// <summary>
    /// Gets or sets the unique identifier for the crawl job.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the provider foreign key.
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the provider.
    /// </summary>
    public Provider? Provider { get; set; }

    /// <summary>
    /// Gets or sets the optional admin user foreign key who requested the job.
    /// </summary>
    public int? RequestedByAdminUserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the requesting admin user.
    /// </summary>
    public AdminUser? RequestedByAdminUser { get; set; }

    /// <summary>
    /// Gets or sets the initial URL to start crawling from.
    /// </summary>
    public required string StartUrl { get; set; }

    /// <summary>
    /// Gets or sets the optional sitemap URL.
    /// </summary>
    public string? SitemapUrl { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum number of pages to crawl.
    /// </summary>
    public int? MaxPages { get; set; }

    /// <summary>
    /// Gets or sets the current status of the job.
    /// </summary>
    public CrawlJobStatus Status { get; set; } = CrawlJobStatus.Queued;

    /// <summary>
    /// Gets or sets a lease owner identifier used by a worker to claim/execute the job.
    /// </summary>
    public string? LeaseOwner { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration time (UTC). When expired, another worker can reclaim.
    /// </summary>
    public DateTime? LeaseExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets an error message when the job fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the job was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when the job started (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the job completed (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the job was canceled (UTC).
    /// </summary>
    public DateTime? CanceledAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the job was paused (UTC).
    /// </summary>
    public DateTime? PausedAt { get; set; }

    /// <summary>
    /// Gets or sets the admin user who paused the job.
    /// </summary>
    public int? PausedByAdminUserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the admin user who paused the job.
    /// </summary>
    public AdminUser? PausedByAdminUser { get; set; }

    /// <summary>
    /// Gets or sets the crawled pages for this job.
    /// </summary>
    public ICollection<CrawlPage> Pages { get; set; } = [];

    /// <summary>
    /// Gets or sets the extracted products for this job.
    /// </summary>
    public ICollection<CrawlExtractedProduct> ExtractedProducts { get; set; } = [];
}
