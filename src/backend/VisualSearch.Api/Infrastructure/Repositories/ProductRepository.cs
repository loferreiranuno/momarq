using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Product entity operations.
/// </summary>
public sealed class ProductRepository : RepositoryBase<Product, int>, IProductRepository
{
    public ProductRepository(VisualSearchDbContext context) : base(context)
    {
    }

    public override async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        int? providerId = null,
        int? categoryId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(p => p.Provider)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .AsQueryable();

        if (providerId.HasValue)
        {
            query = query.Where(p => p.ProviderId == providerId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.Description != null && p.Description.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IEnumerable<Product>> GetByProviderAsync(int providerId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.ProviderId == providerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.Provider)
            .Include(p => p.Images)
            .Where(p => p.CategoryId == categoryId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetWithImagesAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.Provider)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<int> GetCountByProviderAsync(int providerId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .CountAsync(p => p.ProviderId == providerId, cancellationToken);
    }

    public async Task<int> GetCountByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .CountAsync(p => p.CategoryId == categoryId, cancellationToken);
    }

    public override async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.Provider)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
