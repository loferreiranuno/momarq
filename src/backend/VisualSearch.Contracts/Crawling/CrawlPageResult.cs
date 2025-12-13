namespace VisualSearch.Contracts.Crawling;

/// <summary>
/// Result of crawling a single page.
/// </summary>
public sealed record CrawlPageResult
{
    /// <summary>
    /// The URL that was crawled.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Whether the page was successfully processed.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// HTTP status code returned.
    /// </summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>
    /// Content type of the response.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Page title extracted from HTML.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Products extracted from this page.
    /// </summary>
    public List<ExtractedProductDto> Products { get; init; } = [];

    /// <summary>
    /// New URLs discovered on this page to crawl.
    /// </summary>
    public List<string> DiscoveredUrls { get; init; } = [];

    /// <summary>
    /// SHA-256 hash of the page content.
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// Raw HTML content (optional, for auditing).
    /// </summary>
    public string? Content { get; init; }
}
