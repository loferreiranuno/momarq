namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents a product provider/retailer (e.g., Zara Home, IKEA, H&amp;M Home).
/// </summary>
public class Provider
{
    /// <summary>
    /// Gets or sets the unique identifier for the provider.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the provider name (e.g., "Zara Home").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the URL to the provider's logo image.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the provider's website URL.
    /// </summary>
    public string? WebsiteUrl { get; set; }

    /// <summary>
    /// Gets or sets the crawler strategy type to use for this provider.
    /// Examples: "generic", "sitemap", "api", "custom-zarahome", etc.
    /// When null, the "generic" strategy is used.
    /// </summary>
    public string? CrawlerType { get; set; }

    /// <summary>
    /// Gets or sets the JSON configuration for the crawler strategy.
    /// Contains provider-specific selectors, patterns, and settings.
    /// See <see cref="VisualSearch.Contracts.Crawling.CrawlerConfig"/> for structure.
    /// </summary>
    public string? CrawlerConfigJson { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the provider was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the collection of products from this provider.
    /// </summary>
    public ICollection<Product> Products { get; set; } = [];
}
