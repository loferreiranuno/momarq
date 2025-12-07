using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Setting entity operations.
/// Note: Setting uses Key as primary identifier, not Id.
/// </summary>
public sealed class SettingRepository : RepositoryBase<Setting, int>, ISettingRepository
{
    public SettingRepository(VisualSearchDbContext context) : base(context)
    {
    }

    public override async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        // Settings don't have an Id, they use Key
        return false;
    }

    public async Task<Setting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await GetByKeyAsync(key, cancellationToken);
        return setting?.Value;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await GetByKeyAsync(key, cancellationToken);
        if (setting is null)
        {
            setting = new Setting
            {
                Key = key,
                Value = value,
                Category = "General",
                UpdatedAt = DateTime.UtcNow
            };
            await AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            await SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IDictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await DbSet.ToListAsync(cancellationToken);
        return settings.ToDictionary(s => s.Key, s => s.Value);
    }
}
