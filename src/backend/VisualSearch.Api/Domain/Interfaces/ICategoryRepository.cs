using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Domain.Interfaces;

public interface ICategoryRepository : IRepository<Category, int>
{
    Task<IEnumerable<Category>> GetEnabledForDetectionAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Category>> GetByDetectionEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<Category?> GetByCocoClassIdAsync(int cocoClassId, CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task ToggleDetectionAsync(int id, bool enabled, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a category with its products for validation or cascade operations.
    /// </summary>
    Task<Category?> GetWithProductsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all categories with product counts.
    /// </summary>
    Task<IEnumerable<(Category Category, int ProductCount)>> GetAllWithProductCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a category by ID with product count.
    /// </summary>
    Task<(Category? Category, int ProductCount)> GetByIdWithProductCountAsync(int id, CancellationToken cancellationToken = default);
}
