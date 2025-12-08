using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Domain.Interfaces;

public interface IProductRepository : IRepository<Product, int>
{
    Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        int? providerId = null,
        int? categoryId = null,
        string? search = null,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Product>> GetByProviderAsync(int providerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<Product?> GetWithImagesAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GetCountByProviderAsync(int providerId, CancellationToken cancellationToken = default);
    Task<int> GetCountByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
}
