using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Users;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<PagedResult<ManagedUserResponse>> ListAsync(PageQuery query, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            users = users.Where(x => x.Email.Contains(term) || x.FullName.ToLower().Contains(term));
        }
        var count = await users.CountAsync(ct);
        var items = await users.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ManagedUserResponse(x.Id, x.Email, x.FullName, x.IsActive,
                x.UserRoles.OrderBy(r => r.Role.Name).Select(r => r.Role.Name).ToArray(), x.CreatedAt, x.UpdatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }

    public Task<User?> FindAsync(int id, CancellationToken ct) => db.Users.Include(x => x.UserRoles)
        .ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Role>> GetRolesAsync(string[] names, CancellationToken ct)
    {
        var roles = await db.Roles.Where(x => names.Contains(x.Name)).ToArrayAsync(ct);
        if (roles.Length != names.Length) throw new ConflictException("One or more roles are unavailable.");
        return roles;
    }

    public void Add(User user) => db.Users.Add(user);

    public async Task<User?> ReplaceRolesAsync(int userId, IReadOnlyList<Role> roles, DateTimeOffset updatedAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Read memberships only after obtaining the lock, so a competing replacement
        // sees the entire committed predecessor instead of merging stale role sets.
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (user is null) return null;
        await db.Entry(user).Collection(x => x.UserRoles).Query().Include(x => x.Role).LoadAsync(ct);
        foreach (var old in user.UserRoles.Where(x => roles.All(r => r.Id != x.RoleId)).ToArray())
        {
            db.UserRoles.Remove(old);
            user.UserRoles.Remove(old);
        }
        foreach (var role in roles.Where(x => user.UserRoles.All(ur => ur.RoleId != x.Id)))
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        user.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return user;
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_email" })
        { throw new ConflictException("Email is already in use."); }
    }
}
