namespace VisualSearch.Api.Domain.Interfaces;

public interface ISettingsService
{
    Task<T?> GetSettingAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetSettingAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    Task<IDictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsFeatureEnabledAsync(string featureKey, CancellationToken cancellationToken = default);
}
