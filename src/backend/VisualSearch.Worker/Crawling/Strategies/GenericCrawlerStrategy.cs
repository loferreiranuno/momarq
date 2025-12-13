using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Worker.Crawling.Strategies;

/// <summary>
/// Generic HTML-based crawler strategy.
/// Uses CSS selectors from configuration to extract content.
/// Falls back to common patterns (JSON-LD, OpenGraph, meta tags) when selectors not provided.
/// </summary>
public sealed class GenericCrawlerStrategy : ICrawlerStrategy
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IProductExtractor _productExtractor;
    private readonly ILogger<GenericCrawlerStrategy> _logger;

    public string CrawlerType => CrawlerTypes.Generic;

    public GenericCrawlerStrategy(
        IHttpClientFactory httpClientFactory,
        IProductExtractor productExtractor,
        ILogger<GenericCrawlerStrategy> logger)
    {
        _httpClientFactory = httpClientFactory;
        _productExtractor = productExtractor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> DiscoverUrlsAsync(
        string startUrl,
        string? sitemapUrl,
        CrawlerConfig config,
        CancellationToken cancellationToken = default)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Try sitemap first if provided
        if (!string.IsNullOrWhiteSpace(sitemapUrl))
        {
            var sitemapUrls = await DiscoverFromSitemapAsync(sitemapUrl, config, cancellationToken);
            foreach (var url in sitemapUrls)
            {
                urls.Add(url);
            }
        }

        // Also try common sitemap locations
        if (urls.Count == 0)
        {
            var baseUri = new Uri(startUrl);
            var commonSitemaps = new[]
            {
                new Uri(baseUri, "/sitemap.xml").ToString(),
                new Uri(baseUri, "/sitemap_index.xml").ToString(),
                new Uri(baseUri, "/robots.txt").ToString()
            };

            foreach (var sitemapLocation in commonSitemaps)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var sitemapUrls = await DiscoverFromSitemapAsync(sitemapLocation, config, cancellationToken);
                    foreach (var url in sitemapUrls)
                    {
                        urls.Add(url);
                    }

                    if (urls.Count > 0) break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not fetch sitemap from {Url}", sitemapLocation);
                }
            }
        }

        // If no sitemap found, add the start URL for crawling
        if (urls.Count == 0)
        {
            urls.Add(startUrl);
        }

        // Filter URLs based on include/exclude patterns
        var filteredUrls = FilterUrls(urls, config);

        _logger.LogInformation(
            "Discovered {TotalCount} URLs, {FilteredCount} after filtering",
            urls.Count, filteredUrls.Count);

        return filteredUrls;
    }

    public async Task<CrawlPageResult> CrawlPageAsync(
        string url,
        CrawlerConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Crawler");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent);

            using var response = await client.GetAsync(url, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                return new CrawlPageResult
                {
                    Url = url,
                    Success = false,
                    HttpStatusCode = statusCode,
                    Error = $"HTTP {statusCode}: {response.ReasonPhrase}"
                };
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentHash = ComputeHash(html);

            // Parse HTML
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();

            // Extract products and links
            var products = _productExtractor.ExtractProducts(html, url, config);
            var discoveredUrls = _productExtractor.ExtractLinks(html, url, config);

            return new CrawlPageResult
            {
                Url = url,
                Success = true,
                HttpStatusCode = statusCode,
                ContentType = contentType,
                Title = title,
                Products = products.ToList(),
                DiscoveredUrls = discoveredUrls.ToList(),
                ContentHash = contentHash,
                Content = html // Store for auditing
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error crawling {Url}", url);
            return new CrawlPageResult
            {
                Url = url,
                Success = false,
                Error = $"HTTP error: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("Timeout crawling {Url}", url);
            return new CrawlPageResult
            {
                Url = url,
                Success = false,
                Error = "Request timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling {Url}", url);
            return new CrawlPageResult
            {
                Url = url,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<IReadOnlyList<string>> DiscoverFromSitemapAsync(
        string sitemapUrl,
        CrawlerConfig config,
        CancellationToken cancellationToken)
    {
        var urls = new List<string>();

        try
        {
            var client = _httpClientFactory.CreateClient("Crawler");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent);

            var content = await client.GetStringAsync(sitemapUrl, cancellationToken);

            // Check if it's a robots.txt file
            if (sitemapUrl.EndsWith("robots.txt", StringComparison.OrdinalIgnoreCase))
            {
                var sitemapMatches = Regex.Matches(content, @"Sitemap:\s*(.+)", RegexOptions.IgnoreCase);
                foreach (Match match in sitemapMatches)
                {
                    var nestedSitemapUrl = match.Groups[1].Value.Trim();
                    var nestedUrls = await DiscoverFromSitemapAsync(nestedSitemapUrl, config, cancellationToken);
                    urls.AddRange(nestedUrls);
                }
                return urls;
            }

            // Parse XML sitemap
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            // Check for sitemap index
            var sitemapNodes = doc.DocumentNode.SelectNodes("//sitemap/loc");
            if (sitemapNodes != null)
            {
                foreach (var node in sitemapNodes.Take(50)) // Limit nested sitemaps
                {
                    var nestedUrl = node.InnerText.Trim();
                    var nestedUrls = await DiscoverFromSitemapAsync(nestedUrl, config, cancellationToken);
                    urls.AddRange(nestedUrls);
                }
                return urls;
            }

            // Regular sitemap with URLs
            var urlNodes = doc.DocumentNode.SelectNodes("//url/loc");
            if (urlNodes != null)
            {
                foreach (var node in urlNodes)
                {
                    var pageUrl = node.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(pageUrl))
                    {
                        urls.Add(pageUrl);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error parsing sitemap {Url}", sitemapUrl);
        }

        return urls;
    }

    private static List<string> FilterUrls(IEnumerable<string> urls, CrawlerConfig config)
    {
        var result = new List<string>();

        foreach (var url in urls)
        {
            // Check exclude patterns first
            if (config.ExcludePatterns.Count > 0)
            {
                var excluded = config.ExcludePatterns.Any(pattern =>
                    Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase));
                if (excluded) continue;
            }

            // Check include patterns if specified
            if (config.IncludePatterns.Count > 0)
            {
                var included = config.IncludePatterns.Any(pattern =>
                    Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase));
                if (!included) continue;
            }

            result.Add(url);
        }

        return result;
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
