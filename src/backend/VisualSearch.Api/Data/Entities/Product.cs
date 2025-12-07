namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents a product from a provider's catalog.
/// </summary>
public class Product
{
    /// <summary>
    /// Gets or sets the unique identifier for the product.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the provider's foreign key.
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the provider.
    /// </summary>
    public Provider? Provider { get; set; }

    /// <summary>
    /// Gets or sets the external identifier from the provider (SKU/article number).
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the product description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the product price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the currency code (e.g., "EUR", "USD").
    /// </summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Gets or sets the product category (e.g., "sofa", "lamp", "rug").
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the category.
    /// </summary>
    public Category? Category { get; set; }

    /// <summary>
    /// Gets or sets the URL to the product page on the provider's website.
    /// </summary>
    public string? ProductUrl { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the product was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the collection of images for this product.
    /// </summary>
    public ICollection<ProductImage> Images { get; set; } = [];
}
