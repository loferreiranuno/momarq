using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Contracts.DTOs;
using Pgvector;

namespace VisualSearch.Api.Domain.Interfaces;

/// <summary>
/// Repository interface for ProductImage entity operations including vector similarity search.
/// </summary>
public interface IProductImageRepository : IRepository<ProductImage, int>
{
    /// <summary>
    /// Gets all images for a specific product.
    /// </summary>
    Task<IEnumerable<ProductImage>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for similar images using vector similarity (single embedding).
    /// Uses projection for optimal performance.
    /// </summary>
    Task<IEnumerable<ProductImageSearchResult>> SearchByVectorAsync(
        Vector embedding, 
        int limit = 10,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for similar images using multiple embeddings with deduplication.
    /// Uses projection for optimal performance.
    /// </summary>
    /// <param name="embeddings">List of embedding vectors to search.</param>
    /// <param name="limitPerEmbedding">Maximum results per embedding before deduplication.</param>
    /// <param name="totalLimit">Maximum total results after deduplication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of unique ProductImageSearchResult ordered by distance.</returns>
    Task<List<ProductImageSearchResult>> SearchByVectorsAsync(
        List<Vector> embeddings,
        int limitPerEmbedding = 10,
        int totalLimit = 20,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the primary image for a product.
    /// </summary>
    Task<ProductImage?> GetPrimaryImageAsync(int productId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets the primary image for a product.
    /// </summary>
    Task SetPrimaryImageAsync(int productId, int imageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the total count of product images.
    /// </summary>
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
}
