using Pgvector;

namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents an image associated with a product, including its CLIP embedding for similarity search.
/// </summary>
public class ProductImage
{
    /// <summary>
    /// Gets or sets the unique identifier for the product image.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the product's foreign key.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the product.
    /// </summary>
    public Product? Product { get; set; }

    /// <summary>
    /// Gets or sets the URL to the image.
    /// </summary>
    public required string ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the CLIP embedding vector (512 dimensions) for similarity search.
    /// </summary>
    public Vector? Embedding { get; set; }

    /// <summary>
    /// Gets or sets whether this is the primary/main image for the product.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the image was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
