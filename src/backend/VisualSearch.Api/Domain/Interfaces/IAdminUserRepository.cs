using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Domain.Interfaces;

public interface IAdminUserRepository : IRepository<AdminUser, int>
{
    Task<AdminUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
}
