using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Domain.Interfaces;

public interface ICategoryRepository : IRepository<Category, int>
{
    Task<IEnumerable<Category>> GetEnabledForDetectionAsync(CancellationToken cancellationToken = default);
    Task<Category?> GetByCocoClassIdAsync(int cocoClassId, CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task ToggleDetectionAsync(int id, bool enabled, CancellationToken cancellationToken = default);
}
