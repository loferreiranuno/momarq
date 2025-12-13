using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Worker.Crawling;

/// <summary>
/// Defines the contract for extracting products from HTML content.
/// Different providers may need different extraction logic.
/// </summary>
public interface IProductExtractor
{
    /// <summary>
    /// Extracts products from HTML content.
    /// </summary>
    /// <param name="html">The HTML content to extract from.</param>
    /// <param name="pageUrl">The URL of the page (for resolving relative URLs).</param>
    /// <param name="config">Crawler configuration with selectors.</param>
    /// <returns>List of extracted products.</returns>
    IReadOnlyList<ExtractedProductDto> ExtractProducts(
        string html,
        string pageUrl,
        CrawlerConfig config);

    /// <summary>
    /// Extracts links to other pages (pagination, product details).
    /// </summary>
    /// <param name="html">The HTML content to extract from.</param>
    /// <param name="pageUrl">The URL of the page (for resolving relative URLs).</param>
    /// <param name="config">Crawler configuration.</param>
    /// <returns>List of discovered URLs.</returns>
    IReadOnlyList<string> ExtractLinks(
        string html,
        string pageUrl,
        CrawlerConfig config);
}
