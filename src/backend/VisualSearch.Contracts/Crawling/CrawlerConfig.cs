namespace VisualSearch.Contracts.Crawling;

/// <summary>
/// Configuration for a crawler strategy.
/// Stored as JSON in Provider.CrawlerConfigJson.
/// </summary>
public class CrawlerConfig
{
    /// <summary>
    /// The type of crawler strategy to use.
    /// </summary>
    public string CrawlerType { get; set; } = CrawlerTypes.Generic;

    /// <summary>
    /// Delay between requests in milliseconds to respect rate limits.
    /// </summary>
    public int RequestDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum concurrent requests.
    /// </summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>
    /// Whether to respect robots.txt directives.
    /// </summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>
    /// User agent string to use for requests.
    /// </summary>
    public string UserAgent { get; set; } = "VisualSearchBot/1.0 (+https://github.com/loferreiranuno/momarq)";

    /// <summary>
    /// CSS selector for product containers on listing pages.
    /// </summary>
    public string? ProductContainerSelector { get; set; }

    /// <summary>
    /// CSS selector for product links.
    /// </summary>
    public string? ProductLinkSelector { get; set; }

    /// <summary>
    /// CSS selector for product name.
    /// </summary>
    public string? ProductNameSelector { get; set; }

    /// <summary>
    /// CSS selector for product price.
    /// </summary>
    public string? ProductPriceSelector { get; set; }

    /// <summary>
    /// CSS selector for product description.
    /// </summary>
    public string? ProductDescriptionSelector { get; set; }

    /// <summary>
    /// CSS selector for product images.
    /// </summary>
    public string? ProductImageSelector { get; set; }

    /// <summary>
    /// CSS selector for pagination/next page links.
    /// </summary>
    public string? PaginationSelector { get; set; }

    /// <summary>
    /// URL patterns to include (regex). Empty means all.
    /// </summary>
    public List<string> IncludePatterns { get; set; } = [];

    /// <summary>
    /// URL patterns to exclude (regex).
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = [];

    /// <summary>
    /// Additional provider-specific settings as key-value pairs.
    /// </summary>
    public Dictionary<string, string> CustomSettings { get; set; } = [];
}

/// <summary>
/// Well-known crawler type identifiers.
/// </summary>
public static class CrawlerTypes
{
    /// <summary>Generic HTML crawler with CSS selectors.</summary>
    public const string Generic = "generic";

    /// <summary>Crawler that extracts JSON-LD structured data.</summary>
    public const string JsonLd = "json-ld";

    /// <summary>Sitemap-based crawler that discovers URLs from sitemap.xml.</summary>
    public const string Sitemap = "sitemap";

    /// <summary>API-based crawler for providers with public APIs.</summary>
    public const string Api = "api";

    /// <summary>Zara Home crawler with Playwright for JavaScript rendering.</summary>
    public const string ZaraHome = "zarahome";
}
