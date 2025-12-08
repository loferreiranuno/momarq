using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Category entity operations.
/// </summary>
public sealed class CategoryRepository : RepositoryBase<Category, int>, ICategoryRepository
{
    public CategoryRepository(VisualSearchDbContext context) : base(context)
    {
    }

    public override async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetEnabledForDetectionAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(c => c.DetectionEnabled)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetByDetectionEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(c => c.DetectionEnabled == enabled)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetByCocoClassIdAsync(int cocoClassId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(c => c.CocoClassId == cocoClassId, cancellationToken);
    }

    public async Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken);
    }

    public async Task ToggleDetectionAsync(int id, bool enabled, CancellationToken cancellationToken = default)
    {
        var category = await GetByIdAsync(id, cancellationToken);
        if (category is not null)
        {
            category.DetectionEnabled = enabled;
            await SaveChangesAsync(cancellationToken);
        }
    }

    public override async Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetWithProductsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<(Category Category, int ProductCount)>> GetAllWithProductCountsAsync(CancellationToken cancellationToken = default)
    {
        var results = await DbSet
            .OrderBy(c => c.Name)
            .Select(c => new { Category = c, ProductCount = c.Products.Count })
            .ToListAsync(cancellationToken);

        return results.Select(r => (r.Category, r.ProductCount));
    }

    public async Task<(Category? Category, int ProductCount)> GetByIdWithProductCountAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .Where(c => c.Id == id)
            .Select(c => new { Category = c, ProductCount = c.Products.Count })
            .FirstOrDefaultAsync(cancellationToken);

        return result is null ? (null, 0) : (result.Category, result.ProductCount);
    }
}
