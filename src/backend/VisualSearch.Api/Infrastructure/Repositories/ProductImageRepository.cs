using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ProductImage entity operations including vector similarity search.
/// </summary>
public sealed class ProductImageRepository : RepositoryBase<ProductImage, int>, IProductImageRepository
{
    public ProductImageRepository(VisualSearchDbContext context) : base(context)
    {
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

    public async Task<IEnumerable<(ProductImage Image, double Distance)>> SearchByVectorAsync(
        Vector embedding,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var results = await DbSet
            .Include(i => i.Product)
            .ThenInclude(p => p!.Provider)
            .Include(i => i.Product)
            .ThenInclude(p => p!.Category)
            .Where(i => i.Embedding != null)
            .Select(i => new
            {
                Image = i,
                Distance = i.Embedding!.CosineDistance(embedding)
            })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return results.Select(r => (r.Image, r.Distance));
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
