using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Worker.Crawling.Strategies;

/// <summary>
/// Default product extractor that uses multiple strategies:
/// 1. Custom CSS selectors from configuration
/// 2. JSON-LD structured data
/// 3. OpenGraph meta tags
/// 4. Common HTML patterns
/// </summary>
public sealed partial class DefaultProductExtractor : IProductExtractor
{
    private readonly ILogger<DefaultProductExtractor> _logger;

    public DefaultProductExtractor(ILogger<DefaultProductExtractor> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ExtractedProductDto> ExtractProducts(
        string html,
        string pageUrl,
        CrawlerConfig config)
    {
        var products = new List<ExtractedProductDto>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Strategy 1: Try JSON-LD first (most reliable structured data)
        var jsonLdProducts = ExtractFromJsonLd(doc, pageUrl);
        if (jsonLdProducts.Count > 0)
        {
            _logger.LogDebug("Extracted {Count} products from JSON-LD on {Url}", jsonLdProducts.Count, pageUrl);
            products.AddRange(jsonLdProducts);
        }

        // Strategy 2: Try custom selectors if configured
        if (!string.IsNullOrWhiteSpace(config.ProductContainerSelector))
        {
            var selectorProducts = ExtractWithSelectors(doc, pageUrl, config);
            if (selectorProducts.Count > 0)
            {
                _logger.LogDebug("Extracted {Count} products using selectors on {Url}", selectorProducts.Count, pageUrl);
                products.AddRange(selectorProducts);
            }
        }

        // Strategy 3: Try OpenGraph for single product pages
        if (products.Count == 0)
        {
            var ogProduct = ExtractFromOpenGraph(doc, pageUrl);
            if (ogProduct != null)
            {
                _logger.LogDebug("Extracted product from OpenGraph on {Url}", pageUrl);
                products.Add(ogProduct);
            }
        }

        // Deduplicate by external ID or URL
        return DeduplicateProducts(products);
    }

    public IReadOnlyList<string> ExtractLinks(
        string html,
        string pageUrl,
        CrawlerConfig config)
    {
        var links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseUri = new Uri(pageUrl);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Extract all anchor links
        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes != null)
        {
            foreach (var anchor in anchorNodes)
            {
                var href = anchor.GetAttributeValue("href", "");
                var absoluteUrl = ResolveUrl(href, baseUri);

                if (absoluteUrl != null && IsSameDomain(absoluteUrl, baseUri))
                {
                    links.Add(absoluteUrl);
                }
            }
        }

        // Also check pagination selectors if configured
        if (!string.IsNullOrWhiteSpace(config.PaginationSelector))
        {
            try
            {
                var paginationNodes = doc.DocumentNode.SelectNodes(config.PaginationSelector);
                if (paginationNodes != null)
                {
                    foreach (var node in paginationNodes)
                    {
                        var href = node.GetAttributeValue("href", "");
                        var absoluteUrl = ResolveUrl(href, baseUri);

                        if (absoluteUrl != null)
                        {
                            links.Add(absoluteUrl);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting pagination links with selector '{Selector}'",
                    config.PaginationSelector);
            }
        }

        return links.ToList();
    }

    private List<ExtractedProductDto> ExtractFromJsonLd(HtmlDocument doc, string pageUrl)
    {
        var products = new List<ExtractedProductDto>();

        var scriptNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (scriptNodes == null) return products;

        foreach (var scriptNode in scriptNodes)
        {
            try
            {
                var json = scriptNode.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(json)) continue;

                using var jsonDoc = JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;

                // Handle array of items
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        var product = ParseJsonLdProduct(item, pageUrl);
                        if (product != null) products.Add(product);
                    }
                }
                else
                {
                    // Handle @graph structure
                    if (root.TryGetProperty("@graph", out var graph))
                    {
                        foreach (var item in graph.EnumerateArray())
                        {
                            var product = ParseJsonLdProduct(item, pageUrl);
                            if (product != null) products.Add(product);
                        }
                    }
                    else
                    {
                        var product = ParseJsonLdProduct(root, pageUrl);
                        if (product != null) products.Add(product);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Failed to parse JSON-LD on {Url}", pageUrl);
            }
        }

        return products;
    }

    private ExtractedProductDto? ParseJsonLdProduct(JsonElement element, string pageUrl)
    {
        // Check if this is a Product type
        if (!element.TryGetProperty("@type", out var typeElement)) return null;

        var type = typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString()
            : typeElement.EnumerateArray().Select(t => t.GetString()).FirstOrDefault();

        if (!string.Equals(type, "Product", StringComparison.OrdinalIgnoreCase)) return null;

        // Extract product data
        var name = element.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(name)) return null;

        var description = element.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
        var sku = element.TryGetProperty("sku", out var skuEl) ? skuEl.GetString() : null;
        var url = element.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : pageUrl;

        // Extract price from offers
        decimal? price = null;
        string? currency = null;

        if (element.TryGetProperty("offers", out var offers))
        {
            var offer = offers.ValueKind == JsonValueKind.Array
                ? offers.EnumerateArray().FirstOrDefault()
                : offers;

            if (offer.ValueKind != JsonValueKind.Undefined)
            {
                if (offer.TryGetProperty("price", out var priceEl))
                {
                    if (priceEl.ValueKind == JsonValueKind.Number)
                    {
                        price = priceEl.GetDecimal();
                    }
                    else if (priceEl.ValueKind == JsonValueKind.String &&
                             decimal.TryParse(priceEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPrice))
                    {
                        price = parsedPrice;
                    }
                }

                if (offer.TryGetProperty("priceCurrency", out var currencyEl))
                {
                    currency = currencyEl.GetString();
                }
            }
        }

        // Extract images
        var images = new List<string>();
        if (element.TryGetProperty("image", out var imageEl))
        {
            if (imageEl.ValueKind == JsonValueKind.String)
            {
                var img = imageEl.GetString();
                if (!string.IsNullOrWhiteSpace(img)) images.Add(img);
            }
            else if (imageEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in imageEl.EnumerateArray())
                {
                    var imgUrl = img.ValueKind == JsonValueKind.String
                        ? img.GetString()
                        : img.TryGetProperty("url", out var u) ? u.GetString() : null;

                    if (!string.IsNullOrWhiteSpace(imgUrl)) images.Add(imgUrl);
                }
            }
            else if (imageEl.TryGetProperty("url", out var imgUrl))
            {
                var img = imgUrl.GetString();
                if (!string.IsNullOrWhiteSpace(img)) images.Add(img);
            }
        }

        // Extract category
        string? category = null;
        if (element.TryGetProperty("category", out var catEl))
        {
            category = catEl.GetString();
        }

        return new ExtractedProductDto
        {
            ExternalId = sku,
            Name = name,
            Description = description,
            Price = price,
            Currency = currency ?? "EUR",
            ProductUrl = url,
            ImageUrls = images,
            Category = category,
            RawJson = element.GetRawText()
        };
    }

    private List<ExtractedProductDto> ExtractWithSelectors(HtmlDocument doc, string pageUrl, CrawlerConfig config)
    {
        var products = new List<ExtractedProductDto>();
        var baseUri = new Uri(pageUrl);

        try
        {
            var containers = doc.DocumentNode.SelectNodes(config.ProductContainerSelector!);
            if (containers == null) return products;

            foreach (var container in containers)
            {
                var name = SelectText(container, config.ProductNameSelector);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var priceText = SelectText(container, config.ProductPriceSelector);
                var price = ParsePrice(priceText);

                var productUrl = !string.IsNullOrWhiteSpace(config.ProductLinkSelector)
                    ? ResolveUrl(SelectAttribute(container, config.ProductLinkSelector, "href"), baseUri)
                    : null;

                var images = new List<string>();
                if (!string.IsNullOrWhiteSpace(config.ProductImageSelector))
                {
                    var imageNodes = container.SelectNodes(config.ProductImageSelector);
                    if (imageNodes != null)
                    {
                        foreach (var imgNode in imageNodes)
                        {
                            var src = imgNode.GetAttributeValue("src", null)
                                ?? imgNode.GetAttributeValue("data-src", null)
                                ?? imgNode.GetAttributeValue("data-lazy-src", null);

                            var resolvedSrc = ResolveUrl(src, baseUri);
                            if (resolvedSrc != null) images.Add(resolvedSrc);
                        }
                    }
                }

                products.Add(new ExtractedProductDto
                {
                    Name = name,
                    Description = SelectText(container, config.ProductDescriptionSelector),
                    Price = price,
                    Currency = "EUR", // Could be extracted from page
                    ProductUrl = productUrl ?? pageUrl,
                    ImageUrls = images
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting products with custom selectors on {Url}", pageUrl);
        }

        return products;
    }

    private ExtractedProductDto? ExtractFromOpenGraph(HtmlDocument doc, string pageUrl)
    {
        var ogType = doc.DocumentNode.SelectSingleNode("//meta[@property='og:type']")?.GetAttributeValue("content", null);

        // Only extract if it looks like a product page
        if (!string.Equals(ogType, "product", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ogType, "og:product", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", null);
        if (string.IsNullOrWhiteSpace(name)) return null;

        var description = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")?.GetAttributeValue("content", null);
        var image = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", null);
        var url = doc.DocumentNode.SelectSingleNode("//meta[@property='og:url']")?.GetAttributeValue("content", null);
        var priceAmount = doc.DocumentNode.SelectSingleNode("//meta[@property='product:price:amount']")?.GetAttributeValue("content", null);
        var priceCurrency = doc.DocumentNode.SelectSingleNode("//meta[@property='product:price:currency']")?.GetAttributeValue("content", null);

        return new ExtractedProductDto
        {
            Name = name,
            Description = description,
            Price = ParsePrice(priceAmount),
            Currency = priceCurrency ?? "EUR",
            ProductUrl = url ?? pageUrl,
            ImageUrls = string.IsNullOrWhiteSpace(image) ? [] : [image]
        };
    }

    private static string? SelectText(HtmlNode container, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;

        try
        {
            var node = container.SelectSingleNode(selector);
            return node?.InnerText?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? SelectAttribute(HtmlNode container, string? selector, string attribute)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;

        try
        {
            var node = container.SelectSingleNode(selector);
            return node?.GetAttributeValue(attribute, null);
        }
        catch
        {
            return null;
        }
    }

    private static decimal? ParsePrice(string? priceText)
    {
        if (string.IsNullOrWhiteSpace(priceText)) return null;

        // Remove currency symbols and normalize
        var cleaned = PriceCleanupRegex().Replace(priceText, "").Trim();

        // Handle European format (1.234,56) vs US format (1,234.56)
        if (cleaned.Contains(',') && cleaned.Contains('.'))
        {
            // European: 1.234,56
            if (cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.'))
            {
                cleaned = cleaned.Replace(".", "").Replace(",", ".");
            }
            else
            {
                cleaned = cleaned.Replace(",", "");
            }
        }
        else if (cleaned.Contains(','))
        {
            // Could be European decimal (1234,56) or US thousands (1,234)
            var parts = cleaned.Split(',');
            if (parts.Length == 2 && parts[1].Length == 2)
            {
                cleaned = cleaned.Replace(",", ".");
            }
            else
            {
                cleaned = cleaned.Replace(",", "");
            }
        }

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var price)
            ? price
            : null;
    }

    private static string? ResolveUrl(string? href, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;

        // Skip javascript and anchor links
        if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("#") ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Uri.TryCreate(baseUri, href, out var absoluteUri))
        {
            return absoluteUri.GetLeftPart(UriPartial.Path); // Remove query/fragment
        }

        return null;
    }

    private static bool IsSameDomain(string url, Uri baseUri)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ExtractedProductDto> DeduplicateProducts(List<ExtractedProductDto> products)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ExtractedProductDto>();

        foreach (var product in products)
        {
            var key = !string.IsNullOrWhiteSpace(product.ExternalId)
                ? $"sku:{product.ExternalId}"
                : $"url:{product.ProductUrl}";

            if (seen.Add(key))
            {
                result.Add(product);
            }
        }

        return result;
    }

    [GeneratedRegex(@"[€$£¥₹R\s]")]
    private static partial Regex PriceCleanupRegex();
}
