using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Services;

/// <summary>
/// Service for managing application settings with in-memory caching and SSE notifications.
/// </summary>
public sealed class SettingsService : IDisposable
{
    private const string CacheKeyPrefix = "setting:";
    private const string AllSettingsCacheKey = "settings:all";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SettingsService> _logger;
    private readonly ConcurrentDictionary<string, StreamWriter> _sseClients = new();
    private readonly SemaphoreSlim _sseLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Service scope factory for creating database contexts.</param>
    /// <param name="cache">Memory cache for settings.</param>
    /// <param name="logger">Logger instance.</param>
    public SettingsService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<SettingsService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets a setting value by key, with caching.
    /// </summary>
    /// <typeparam name="T">The type to convert the setting value to.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">Default value if setting not found.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The setting value or default.</returns>
    public async Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{key}";

        if (_cache.TryGetValue(cacheKey, out T? cachedValue) && cachedValue is not null)
        {
            return cachedValue;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VisualSearchDbContext>();

        var setting = await dbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting is null)
        {
            return defaultValue;
        }

        var value = ConvertValue<T>(setting.Value, setting.Type);
        _cache.Set(cacheKey, value, TimeSpan.FromMinutes(30));

        return value;
    }

    /// <summary>
    /// Gets all settings, with caching.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all settings.</returns>
    public async Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(AllSettingsCacheKey, out IReadOnlyList<Setting>? cachedSettings) && cachedSettings is not null)
        {
            return cachedSettings;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VisualSearchDbContext>();

        var settings = await dbContext.Settings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);

        _cache.Set(AllSettingsCacheKey, (IReadOnlyList<Setting>)settings, TimeSpan.FromMinutes(30));

        return settings;
    }

    /// <summary>
    /// Gets settings by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Settings in the specified category.</returns>
    public async Task<IReadOnlyList<Setting>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var allSettings = await GetAllAsync(cancellationToken);
        return allSettings.Where(s => s.Category == category).ToList();
    }

    /// <summary>
    /// Updates a setting value and broadcasts the change via SSE.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated setting, or null if not found.</returns>
    public async Task<Setting?> UpdateAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VisualSearchDbContext>();

        var setting = await dbContext.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting is null)
        {
            _logger.LogWarning("Setting not found: {Key}", key);
            return null;
        }

        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        InvalidateCache(key);

        // Broadcast change via SSE
        await BroadcastSettingChangeAsync(key, value, cancellationToken);

        _logger.LogInformation("Setting updated: {Key} = {Value}", key, value);

        return setting;
    }

    /// <summary>
    /// Creates a new setting.
    /// </summary>
    /// <param name="setting">The setting to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created setting.</returns>
    public async Task<Setting> CreateAsync(Setting setting, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VisualSearchDbContext>();

        dbContext.Settings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCache();

        _logger.LogInformation("Setting created: {Key} = {Value}", setting.Key, setting.Value);

        return setting;
    }

    /// <summary>
    /// Invalidates the settings cache (for admin force-clear).
    /// </summary>
    /// <param name="key">Specific key to invalidate, or null for all.</param>
    public void InvalidateCache(string? key = null)
    {
        if (key is not null)
        {
            _cache.Remove($"{CacheKeyPrefix}{key}");
        }

        _cache.Remove(AllSettingsCacheKey);
        _logger.LogInformation("Settings cache invalidated{Key}", key is not null ? $" for key: {key}" : "");
    }

    /// <summary>
    /// Registers an SSE client for settings change notifications.
    /// </summary>
    /// <param name="clientId">Unique client identifier.</param>
    /// <param name="writer">The response stream writer.</param>
    public void RegisterSseClient(string clientId, StreamWriter writer)
    {
        _sseClients.TryAdd(clientId, writer);
        _logger.LogDebug("SSE client registered: {ClientId}", clientId);
    }

    /// <summary>
    /// Unregisters an SSE client.
    /// </summary>
    /// <param name="clientId">The client to remove.</param>
    public void UnregisterSseClient(string clientId)
    {
        _sseClients.TryRemove(clientId, out _);
        _logger.LogDebug("SSE client unregistered: {ClientId}", clientId);
    }

    /// <summary>
    /// Broadcasts a settings change to all connected SSE clients.
    /// </summary>
    private async Task BroadcastSettingChangeAsync(string key, string value, CancellationToken cancellationToken)
    {
        if (_sseClients.IsEmpty)
        {
            return;
        }

        var message = $"event: setting-change\ndata: {{\"key\":\"{key}\",\"value\":\"{EscapeJsonString(value)}\"}}\n\n";

        await _sseLock.WaitAsync(cancellationToken);

        try
        {
            var failedClients = new List<string>();

            foreach (var (clientId, writer) in _sseClients)
            {
                try
                {
                    await writer.WriteAsync(message);
                    await writer.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SSE to client {ClientId}", clientId);
                    failedClients.Add(clientId);
                }
            }

            // Remove failed clients
            foreach (var clientId in failedClients)
            {
                UnregisterSseClient(clientId);
            }
        }
        finally
        {
            _sseLock.Release();
        }
    }

    /// <summary>
    /// Broadcasts a cache invalidation event to all SSE clients.
    /// </summary>
    public async Task BroadcastCacheInvalidationAsync(CancellationToken cancellationToken = default)
    {
        InvalidateCache();

        if (_sseClients.IsEmpty)
        {
            return;
        }

        const string message = "event: cache-invalidated\ndata: {}\n\n";

        await _sseLock.WaitAsync(cancellationToken);

        try
        {
            foreach (var (clientId, writer) in _sseClients)
            {
                try
                {
                    await writer.WriteAsync(message);
                    await writer.FlushAsync();
                }
                catch
                {
                    // Ignore errors, will be cleaned up on next broadcast
                }
            }
        }
        finally
        {
            _sseLock.Release();
        }

        _logger.LogInformation("Cache invalidation broadcast sent to {Count} clients", _sseClients.Count);
    }

    private static T ConvertValue<T>(string value, SettingType type)
    {
        var targetType = typeof(T);

        object result = type switch
        {
            SettingType.Integer when targetType == typeof(int) => int.Parse(value),
            SettingType.Integer when targetType == typeof(long) => long.Parse(value),
            SettingType.Boolean when targetType == typeof(bool) => bool.Parse(value),
            SettingType.Decimal when targetType == typeof(decimal) => decimal.Parse(value),
            SettingType.Decimal when targetType == typeof(double) => double.Parse(value),
            SettingType.Decimal when targetType == typeof(float) => float.Parse(value),
            _ => value
        };

        return (T)result;
    }

    private static string EscapeJsonString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _sseLock.Dispose();
    }
}
