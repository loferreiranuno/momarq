using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Domain.Interfaces;

public interface IProviderRepository : IRepository<Provider, int>
{
    Task<Provider?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Provider?> GetWithProductsAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Provider>> GetActiveProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a provider with all products and their images for cascade delete.
    /// </summary>
    Task<Provider?> GetWithProductsAndImagesAsync(int id, CancellationToken cancellationToken = default);
}
