namespace VisualSearch.Contracts.Crawling;

/// <summary>
/// Request payload to create a new crawl job.
/// </summary>
public sealed record CreateCrawlJobRequest
{
    /// <summary>Gets the provider id whose website will be crawled.</summary>
    public required int ProviderId { get; init; }

    /// <summary>
    /// Gets an optional entry URL. If not provided, the provider website URL will be used.
    /// </summary>
    public string? StartUrl { get; init; }

    /// <summary>
    /// Gets an optional sitemap URL. If not provided, the worker may try to discover a sitemap.
    /// </summary>
    public string? SitemapUrl { get; init; }

    /// <summary>
    /// Gets the maximum number of pages to crawl for this job.
    /// </summary>
    public int? MaxPages { get; init; }
}
