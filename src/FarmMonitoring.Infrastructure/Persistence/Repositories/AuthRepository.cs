using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class AuthRepository(AppDbContext db, TimeProvider clock) : IAuthRepository
{
    public Task<User?> GetUserByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail, ct);

    public Task<User?> GetUserByIdAsync(int id, CancellationToken ct) =>
        db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task SaveLoginAsync(RefreshToken token, CancellationToken ct)
    {
        db.RefreshTokens.Add(token);
        // Also persists a password rehash on the tracked login user, if needed.
        await db.SaveChangesAsync(ct);
    }

    public async Task<User?> RotateAsync(string oldHash, RefreshToken replacement, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var old = await LockTokenAsync(oldHash, ct);
        // Lock acquisition can wait; validate against the time AFTER the wait.
        var now = clock.GetUtcNow();
        if (old is null || old.RevokedAt is not null || old.ExpiresAt <= now)
            return null;
        var user = await GetUserByIdAsync(old.UserId, ct);
        if (user is null || !user.IsActive)
            return null;

        replacement.UserId = old.UserId;
        var lifetime = replacement.ExpiresAt - replacement.CreatedAt;
        replacement.CreatedAt = now;
        replacement.ExpiresAt = now.Add(lifetime);
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);
        old.RevokedAt = now;
        old.ReplacedByTokenId = replacement.Id;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return user;
    }

    public async Task<bool> RevokeAsync(int userId, string tokenHash, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var token = await LockTokenAsync(tokenHash, ct);
        var now = clock.GetUtcNow();
        if (token is null || token.UserId != userId || token.RevokedAt is not null || token.ExpiresAt <= now)
            return false;
        token.RevokedAt = now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private Task<RefreshToken?> LockTokenAsync(string hash, CancellationToken ct) =>
        // Parameterized SQL; PostgreSQL serializes competing refresh/logout operations
        // on the same token. A waiter reads the committed revocation before proceeding.
        db.RefreshTokens.FromSqlInterpolated($"SELECT * FROM refresh_tokens WHERE token_hash = {hash} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
}
