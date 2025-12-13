namespace VisualSearch.Contracts.Crawling;

/// <summary>
/// Represents the review/import status of an extracted product.
/// </summary>
public enum ExtractedProductStatus
{
    /// <summary>Product is awaiting review.</summary>
    Pending = 0,

    /// <summary>Product has been approved and imported to the products table.</summary>
    Approved = 1,

    /// <summary>Product was rejected during review.</summary>
    Rejected = 2,

    /// <summary>Product was marked as a duplicate of an existing product.</summary>
    Duplicate = 3
}
