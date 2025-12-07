using VisualSearch.Api.Data.Entities;
using Pgvector;

namespace VisualSearch.Api.Domain.Interfaces;

public interface IProductImageRepository : IRepository<ProductImage, int>
{
    Task<IEnumerable<ProductImage>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IEnumerable<(ProductImage Image, double Distance)>> SearchByVectorAsync(
        Vector embedding, 
        int limit = 10,
        CancellationToken cancellationToken = default);
    Task<ProductImage?> GetPrimaryImageAsync(int productId, CancellationToken cancellationToken = default);
    Task SetPrimaryImageAsync(int productId, int imageId, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
}
