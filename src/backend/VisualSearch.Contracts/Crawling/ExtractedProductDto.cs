namespace VisualSearch.Contracts.Crawling;

/// <summary>
/// Represents a product extracted during crawling.
/// This is a DTO used for transferring data between crawler and storage.
/// </summary>
public sealed record ExtractedProductDto
{
    /// <summary>
    /// Provider's external identifier for the product (SKU, article number).
    /// </summary>
    public string? ExternalId { get; init; }

    /// <summary>
    /// Product name/title.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Product description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Product price.
    /// </summary>
    public decimal? Price { get; init; }

    /// <summary>
    /// Currency code (EUR, USD, etc.).
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>
    /// URL to the product page.
    /// </summary>
    public string? ProductUrl { get; init; }

    /// <summary>
    /// List of product image URLs.
    /// </summary>
    public List<string> ImageUrls { get; init; } = [];

    /// <summary>
    /// Category or categories the product belongs to.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Raw JSON data for auditing purposes.
    /// </summary>
    public string? RawJson { get; init; }
}
