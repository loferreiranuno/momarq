using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ProductImage entity operations including vector similarity search.
/// Uses projection queries for optimal performance.
/// </summary>
public sealed class ProductImageRepository : RepositoryBase<ProductImage, int>, IProductImageRepository
{
    private readonly int _hnswEfSearch;

    public ProductImageRepository(VisualSearchDbContext context, IOptions<ModelSettings> settings) : base(context)
    {
        _hnswEfSearch = settings.Value.Search.HnswEfSearch;
    }

    public override async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<ProductImage>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Searches for similar images using vector similarity with projection.
    /// Uses HNSW index tuning for faster queries.
    /// </summary>
    public async Task<IEnumerable<ProductImageSearchResult>> SearchByVectorAsync(
        Vector embedding,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        // Set HNSW ef_search parameter for query-time recall/speed tradeoff
        // PostgreSQL SET command doesn't support parameterized queries,
        // so we use ExecuteSqlRawAsync with a validated integer value
        await Context.Database.ExecuteSqlRawAsync(
            $"SET LOCAL hnsw.ef_search = {_hnswEfSearch:D}", 
            cancellationToken);

        // Use projection to avoid loading full entity graphs
        var results = await DbSet
            .Where(i => i.Embedding != null)
            .Select(i => new ProductImageSearchResult
            {
                ImageId = i.Id,
                ProductId = i.ProductId,
                ImageUrl = i.ImageUrl,
                ProductName = i.Product != null ? i.Product.Name : "Unknown",
                Price = i.Product != null ? i.Product.Price : null,
                Currency = i.Product != null ? i.Product.Currency : null,
                ProductUrl = i.Product != null ? i.Product.ProductUrl : null,
                ProviderName = i.Product != null && i.Product.Provider != null ? i.Product.Provider.Name : "Unknown",
                CategoryName = i.Product != null && i.Product.Category != null ? i.Product.Category.Name : null,
                Distance = i.Embedding!.CosineDistance(embedding)
            })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return results;
    }

    /// <summary>
    /// Searches for similar images using multiple embeddings with deduplication.
    /// Uses projection and HNSW tuning for optimal performance.
    /// </summary>
    public async Task<List<ProductImageSearchResult>> SearchByVectorsAsync(
        List<Vector> embeddings,
        int limitPerEmbedding = 10,
        int totalLimit = 20,
        CancellationToken cancellationToken = default)
    {
        if (embeddings.Count == 0)
        {
            return [];
        }

        // Set HNSW ef_search parameter once for all queries in this session
        // PostgreSQL SET command doesn't support parameterized queries,
        // so we use ExecuteSqlRawAsync with a validated integer value
        // The value comes from configuration and is validated as an integer
        await Context.Database.ExecuteSqlRawAsync(
            $"SET LOCAL hnsw.ef_search = {_hnswEfSearch:D}", 
            cancellationToken);

        // For single embedding, use the simpler method with deduplication
        if (embeddings.Count == 1)
        {
            var singleResult = await SearchByVectorCoreAsync(embeddings[0], totalLimit * 3, cancellationToken);
            return DeduplicateByProduct(singleResult, totalLimit);
        }

        // Execute queries sequentially to avoid DbContext threading issues
        var allResults = new List<ProductImageSearchResult>();
        
        foreach (var embedding in embeddings)
        {
            var results = await SearchByVectorCoreAsync(embedding, limitPerEmbedding, cancellationToken);
            allResults.AddRange(results);
        }

        // Deduplicate by ProductId - keep the best (lowest distance) match per product
        return DeduplicateByProduct(allResults, totalLimit);
    }

    /// <summary>
    /// Core vector search with projection (no HNSW setup - caller must set it).
    /// </summary>
    private async Task<List<ProductImageSearchResult>> SearchByVectorCoreAsync(
        Vector embedding,
        int limit,
        CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(i => i.Embedding != null)
            .Select(i => new ProductImageSearchResult
            {
                ImageId = i.Id,
                ProductId = i.ProductId,
                ImageUrl = i.ImageUrl,
                ProductName = i.Product != null ? i.Product.Name : "Unknown",
                Price = i.Product != null ? i.Product.Price : null,
                Currency = i.Product != null ? i.Product.Currency : null,
                ProductUrl = i.Product != null ? i.Product.ProductUrl : null,
                ProviderName = i.Product != null && i.Product.Provider != null ? i.Product.Provider.Name : "Unknown",
                CategoryName = i.Product != null && i.Product.Category != null ? i.Product.Category.Name : null,
                Distance = i.Embedding!.CosineDistance(embedding)
            })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Deduplicates results by ProductId, keeping the best match per product.
    /// </summary>
    private static List<ProductImageSearchResult> DeduplicateByProduct(
        List<ProductImageSearchResult> results,
        int limit)
    {
        return results
            .GroupBy(r => r.ProductId)
            .Select(g => g.MinBy(x => x.Distance)!)
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToList();
    }

    public async Task<ProductImage?> GetPrimaryImageAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.IsPrimary, cancellationToken);
    }

    public async Task SetPrimaryImageAsync(int productId, int imageId, CancellationToken cancellationToken = default)
    {
        // Reset all images for the product to non-primary
        var productImages = await DbSet
            .Where(i => i.ProductId == productId)
            .ToListAsync(cancellationToken);

        foreach (var image in productImages)
        {
            image.IsPrimary = image.Id == imageId;
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(cancellationToken);
    }
}
