using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Provider entity operations.
/// </summary>
public sealed class ProviderRepository : RepositoryBase<Provider, int>, IProviderRepository
{
    public ProviderRepository(VisualSearchDbContext context) : base(context)
    {
    }

    public override async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Provider?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower(), cancellationToken);
    }

    public async Task<Provider?> GetWithProductsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.Products)
            .ThenInclude(pr => pr.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Provider>> GetActiveProvidersAsync(CancellationToken cancellationToken = default)
    {
        // Note: Provider entity doesn't have IsActive field currently
        // This method returns all providers ordered by name
        return await DbSet
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public override async Task<IEnumerable<Provider>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
