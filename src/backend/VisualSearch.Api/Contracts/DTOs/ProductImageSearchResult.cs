namespace VisualSearch.Api.Contracts.DTOs;

/// <summary>
/// Lightweight DTO for vector search results.
/// Uses projection to avoid loading full entity graphs.
/// </summary>
public sealed record ProductImageSearchResult
{
    /// <summary>Gets or sets the image ID.</summary>
    public int ImageId { get; init; }

    /// <summary>Gets or sets the product ID.</summary>
    public int ProductId { get; init; }

    /// <summary>Gets or sets the image URL.</summary>
    public required string ImageUrl { get; init; }

    /// <summary>Gets or sets the product name.</summary>
    public required string ProductName { get; init; }

    /// <summary>Gets or sets the product price.</summary>
    public decimal? Price { get; init; }

    /// <summary>Gets or sets the currency.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets or sets the product URL.</summary>
    public string? ProductUrl { get; init; }

    /// <summary>Gets or sets the provider name.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Gets or sets the category name.</summary>
    public string? CategoryName { get; init; }

    /// <summary>Gets or sets the cosine distance (lower = more similar).</summary>
    public double Distance { get; init; }

    /// <summary>
    /// Converts to SearchResultDto for API responses.
    /// </summary>
    public SearchResultDto ToSearchResultDto() => new(
        ProductId: ProductId,
        ProductName: ProductName,
        Price: Price,
        Currency: Currency,
        ProviderName: ProviderName,
        CategoryName: CategoryName,
        ImageUrl: ImageUrl,
        ProductUrl: ProductUrl,
        Similarity: 1 - Distance
    );
}
