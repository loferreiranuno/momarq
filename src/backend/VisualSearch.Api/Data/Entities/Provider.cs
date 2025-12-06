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
    /// Gets or sets the date and time when the provider was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the collection of products from this provider.
    /// </summary>
    public ICollection<Product> Products { get; set; } = [];
}
