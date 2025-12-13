using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Playwright;
using VisualSearch.Contracts.Crawling;
using VisualSearch.Worker.Services;

namespace VisualSearch.Worker.Crawling.Strategies;

/// <summary>
/// Crawler strategy for Zara Home website.
/// Uses sitemap for URL discovery and Playwright for JavaScript-rendered content.
/// 
/// Key behaviors:
/// - Discovers products from official sitemaps (sitemap-products-zh-*.xml.gz)
/// - Uses Playwright to render pages (Zara Home blocks direct HTTP)
/// - Extracts data from window.__PRELOADED_STATE__ or DOM fallback
/// - Implements rate limiting (2-5s delays) to avoid bot detection
/// </summary>
public class ZaraHomeCrawlerStrategy : ICrawlerStrategy
{
    private readonly ILogger<ZaraHomeCrawlerStrategy> _logger;
    private readonly PlaywrightBrowserService _browserService;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    // Zara Home sitemap for Spain (can be configured per country)
    private const string DefaultSitemapUrl = "https://www.zarahome.com/8/info/sitemaps/sitemap-products-zh-es-0.xml.gz";

    public string CrawlerType => CrawlerTypes.ZaraHome;

    public ZaraHomeCrawlerStrategy(
        ILogger<ZaraHomeCrawlerStrategy> logger,
        PlaywrightBrowserService browserService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _browserService = browserService;
        _httpClient = httpClientFactory.CreateClient("ZaraHome");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> DiscoverUrlsAsync(
        string startUrl,
        string? sitemapUrl,
        CrawlerConfig config,
        CancellationToken cancellationToken = default)
    {
        // Prefer configured sitemap, then custom setting, then default
        var effectiveSitemapUrl = sitemapUrl;
        
        if (string.IsNullOrWhiteSpace(effectiveSitemapUrl) &&
            config.CustomSettings.TryGetValue("SitemapUrl", out var customSitemap))
        {
            effectiveSitemapUrl = customSitemap;
        }

        if (string.IsNullOrWhiteSpace(effectiveSitemapUrl))
        {
            effectiveSitemapUrl = DefaultSitemapUrl;
        }

        _logger.LogInformation("Discovering URLs from Zara Home sitemap: {Url}", effectiveSitemapUrl);

        List<string> urls;
        try
        {
            urls = await ParseSitemapAsync(effectiveSitemapUrl, cancellationToken);
            _logger.LogInformation("Found {Count} product URLs in sitemap", urls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse sitemap, falling back to start URL");
            urls = [startUrl];
        }

        // Apply max pages limit if configured via CustomSettings
        if (config.CustomSettings.TryGetValue("MaxPages", out var maxPagesStr) &&
            int.TryParse(maxPagesStr, out var maxPages) &&
            maxPages > 0 &&
            urls.Count > maxPages)
        {
            urls = urls.Take(maxPages).ToList();
        }

        return urls;
    }

    /// <inheritdoc />
    public async Task<CrawlPageResult> CrawlPageAsync(
        string url,
        CrawlerConfig config,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Crawling Zara Home page: {Url}", url);

        // Rate limiting - random delay between requests
        var delay = _random.Next(config.RequestDelayMs, config.RequestDelayMs * 3);
        await Task.Delay(delay, cancellationToken);

        await using var lease = await _browserService.AcquireContextAsync(cancellationToken);
        var page = await lease.Context.NewPageAsync();

        try
        {
            // Navigate and wait for content
            var response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

            if (response is null || !response.Ok)
            {
                return new CrawlPageResult
                {
                    Url = url,
                    Success = false,
                    HttpStatusCode = response?.Status,
                    Error = $"Page returned status: {response?.Status}"
                };
            }

            // Wait for product info to load
            try
            {
                await page.WaitForSelectorAsync("[data-qa-qualifier='product-detail-info']", 
                    new PageWaitForSelectorOptions { Timeout = 10000 });
            }
            catch (TimeoutException)
            {
                // Product selector not found, might be a category page or different layout
                _logger.LogDebug("Product detail selector not found on {Url}", url);
            }

            // Get page content for hash
            var html = await page.ContentAsync();
            var contentHash = ComputeHash(html);
            var title = await page.TitleAsync();

            // Try extracting from __PRELOADED_STATE__ first (most reliable)
            var products = await ExtractFromPreloadedStateAsync(page, url);

            if (products.Count == 0)
            {
                // Fallback to DOM extraction
                _logger.LogDebug("Preloaded state not found, falling back to DOM extraction");
                products = await ExtractFromDomAsync(page, url);
            }

            // Find pagination links (for category pages)
            var nextUrls = await ExtractPaginationLinksAsync(page);

            return new CrawlPageResult
            {
                Url = url,
                Success = true,
                HttpStatusCode = response.Status,
                ContentType = "text/html",
                Title = title,
                Products = products,
                DiscoveredUrls = nextUrls,
                ContentHash = contentHash,
                Content = html
            };
        }
        catch (TimeoutException)
        {
            return new CrawlPageResult
            {
                Url = url,
                Success = false,
                Error = "Page load timeout - possible bot detection"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling Zara Home page: {Url}", url);
            return new CrawlPageResult
            {
                Url = url,
                Success = false,
                Error = ex.Message
            };
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Parses Zara Home's gzipped sitemap XML.
    /// </summary>
    private async Task<List<string>> ParseSitemapAsync(string sitemapUrl, CancellationToken cancellationToken)
    {
        var urls = new List<string>();

        // Download gzipped sitemap
        using var response = await _httpClient.GetAsync(sitemapUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var compressedStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var decompressedStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        
        var doc = await XDocument.LoadAsync(decompressedStream, LoadOptions.None, cancellationToken);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        foreach (var loc in doc.Descendants(ns + "loc"))
        {
            var url = loc.Value;
            // Filter for product URLs (contain "-l" pattern with product ID)
            if (!string.IsNullOrWhiteSpace(url) && url.Contains("-l"))
            {
                urls.Add(url);
            }
        }

        return urls;
    }

    /// <summary>
    /// Extracts product data from window.__PRELOADED_STATE__ JavaScript variable.
    /// This is the most reliable method as it contains structured JSON data.
    /// </summary>
    private async Task<List<ExtractedProductDto>> ExtractFromPreloadedStateAsync(
        IPage page,
        string url)
    {
        var products = new List<ExtractedProductDto>();

        try
        {
            // Execute JavaScript to get preloaded state
            var stateJson = await page.EvaluateAsync<string?>(
                "window.__PRELOADED_STATE__ ? JSON.stringify(window.__PRELOADED_STATE__) : null");

            if (string.IsNullOrEmpty(stateJson))
            {
                return products;
            }

            using var doc = JsonDocument.Parse(stateJson);
            var root = doc.RootElement;

            // Navigate to product data - structure may vary
            if (root.TryGetProperty("product", out var productElement) ||
                root.TryGetProperty("productDetail", out productElement))
            {
                var product = ParseProductFromJson(productElement, url);
                if (product is not null)
                {
                    products.Add(product);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract from preloaded state");
        }

        return products;
    }

    /// <summary>
    /// Parses product information from Zara Home's JSON structure.
    /// </summary>
    private ExtractedProductDto? ParseProductFromJson(JsonElement element, string url)
    {
        try
        {
            var name = element.TryGetProperty("name", out var nameEl) 
                ? nameEl.GetString() 
                : null;

            var externalId = element.TryGetProperty("id", out var idEl)
                ? idEl.ToString()
                : ExtractProductIdFromUrl(url);

            decimal? price = null;
            if (element.TryGetProperty("price", out var priceEl))
            {
                price = priceEl.ValueKind == JsonValueKind.Number 
                    ? priceEl.GetDecimal() / 100m // Zara stores prices in cents
                    : null;
            }
            else if (element.TryGetProperty("currentPrice", out var cpEl) && cpEl.ValueKind == JsonValueKind.Number)
            {
                price = cpEl.GetDecimal() / 100m;
            }

            var description = element.TryGetProperty("description", out var descEl)
                ? descEl.GetString()
                : null;

            // Extract images
            var imageUrls = new List<string>();
            if (element.TryGetProperty("images", out var imagesEl) && imagesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in imagesEl.EnumerateArray())
                {
                    var imgUrl = img.TryGetProperty("url", out var urlEl) 
                        ? urlEl.GetString()
                        : img.TryGetProperty("src", out var srcEl)
                            ? srcEl.GetString()
                            : null;
                    
                    if (!string.IsNullOrEmpty(imgUrl))
                    {
                        imageUrls.Add(imgUrl);
                    }
                }
            }
            else if (element.TryGetProperty("media", out imagesEl) && imagesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in imagesEl.EnumerateArray())
                {
                    var imgUrl = img.TryGetProperty("url", out var urlEl) 
                        ? urlEl.GetString()
                        : img.TryGetProperty("src", out var srcEl)
                            ? srcEl.GetString()
                            : null;
                    
                    if (!string.IsNullOrEmpty(imgUrl))
                    {
                        imageUrls.Add(imgUrl);
                    }
                }
            }

            // Extract category
            var category = element.TryGetProperty("category", out var catEl)
                ? catEl.GetString()
                : element.TryGetProperty("familyName", out var famEl)
                    ? famEl.GetString()
                    : null;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(externalId))
            {
                return null;
            }

            return new ExtractedProductDto
            {
                Name = name,
                ExternalId = externalId,
                Price = price,
                Currency = "EUR",
                Description = description,
                Category = category,
                ImageUrls = imageUrls,
                ProductUrl = url
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse product JSON");
            return null;
        }
    }

    /// <summary>
    /// Fallback DOM extraction when __PRELOADED_STATE__ is unavailable.
    /// </summary>
    private async Task<List<ExtractedProductDto>> ExtractFromDomAsync(
        IPage page,
        string url)
    {
        var products = new List<ExtractedProductDto>();

        try
        {
            var name = await page.TextContentAsync("h1.product-detail-info__header-name");
            var priceText = await page.TextContentAsync("[data-qa-qualifier='product-detail-info-price-amount']");
            var description = await page.TextContentAsync(".expandable-text__inner-content");

            // Parse price (format: "12,99 €" or "12.99 €")
            decimal? price = null;
            if (!string.IsNullOrEmpty(priceText))
            {
                var cleanPrice = priceText
                    .Replace("€", "")
                    .Replace(" ", "")
                    .Replace(",", ".")
                    .Trim();
                
                if (decimal.TryParse(cleanPrice, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                {
                    price = parsed;
                }
            }

            // Extract images
            var imageUrls = new List<string>();
            var images = await page.QuerySelectorAllAsync("picture.media-image img");
            foreach (var img in images)
            {
                var src = await img.GetAttributeAsync("src");
                if (!string.IsNullOrEmpty(src))
                {
                    imageUrls.Add(src);
                }
            }

            var externalId = ExtractProductIdFromUrl(url);

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(externalId))
            {
                products.Add(new ExtractedProductDto
                {
                    Name = name.Trim(),
                    ExternalId = externalId,
                    Price = price,
                    Currency = "EUR",
                    Description = description?.Trim(),
                    ImageUrls = imageUrls,
                    ProductUrl = url
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DOM extraction failed for {Url}", url);
        }

        return products;
    }

    /// <summary>
    /// Extracts product ID from URL pattern: /es/product-name-l12345678
    /// </summary>
    private static string? ExtractProductIdFromUrl(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(url, @"-l(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extracts pagination links from category pages.
    /// </summary>
    private static async Task<List<string>> ExtractPaginationLinksAsync(IPage page)
    {
        var links = new List<string>();
        
        try
        {
            var nextButton = await page.QuerySelectorAsync("a[data-qa-qualifier='pagination-next']");
            if (nextButton is not null)
            {
                var href = await nextButton.GetAttributeAsync("href");
                if (!string.IsNullOrEmpty(href))
                {
                    links.Add(href);
                }
            }
        }
        catch
        {
            // Pagination not present on this page type
        }

        return links;
    }

    /// <summary>
    /// Computes SHA256 hash of content.
    /// </summary>
    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
