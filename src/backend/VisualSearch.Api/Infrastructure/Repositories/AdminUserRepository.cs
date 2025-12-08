using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for AdminUser entity operations.
/// </summary>
public sealed class AdminUserRepository : RepositoryBase<AdminUser, int>, IAdminUserRepository
{
    public AdminUserRepository(VisualSearchDbContext context) : base(context)
    {
    }

    public override async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<AdminUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken);
    }
}
