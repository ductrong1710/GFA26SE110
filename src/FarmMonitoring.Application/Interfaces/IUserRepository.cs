using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Users;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IUserRepository
{
    Task<PagedResult<ManagedUserResponse>> ListAsync(PageQuery query, CancellationToken ct);
    Task<User?> FindAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Role>> GetRolesAsync(string[] names, CancellationToken ct);
    void Add(User user);
    Task<User?> ReplaceRolesAsync(int userId, IReadOnlyList<Role> roles, DateTimeOffset updatedAt, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
