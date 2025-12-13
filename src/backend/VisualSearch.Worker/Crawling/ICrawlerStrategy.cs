using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Worker.Crawling;

/// <summary>
/// Defines the contract for a crawler strategy.
/// Each provider type can have a different implementation.
/// </summary>
public interface ICrawlerStrategy
{
    /// <summary>
    /// Gets the crawler type identifier this strategy handles.
    /// </summary>
    string CrawlerType { get; }

    /// <summary>
    /// Discovers URLs to crawl from a starting point.
    /// </summary>
    /// <param name="startUrl">The initial URL to start discovery from.</param>
    /// <param name="sitemapUrl">Optional sitemap URL.</param>
    /// <param name="config">Crawler configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of URLs to crawl.</returns>
    Task<IReadOnlyList<string>> DiscoverUrlsAsync(
        string startUrl,
        string? sitemapUrl,
        CrawlerConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crawls a single page and extracts products.
    /// </summary>
    /// <param name="url">The URL to crawl.</param>
    /// <param name="config">Crawler configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing extracted products and discovered URLs.</returns>
    Task<CrawlPageResult> CrawlPageAsync(
        string url,
        CrawlerConfig config,
        CancellationToken cancellationToken = default);
}
