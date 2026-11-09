using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PasswordRecoveryRepository(AppDbContext db, TimeProvider clock) : IPasswordRecoveryRepository
{
    private Task<User?> LockAsync(int id, CancellationToken ct) => db.Users
        .FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);

    public async Task<bool> IssueAsync(User expected, string hash, CancellationToken ct)
    {
        db.Entry(expected).State = EntityState.Detached;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockAsync(expected.Id, ct);
        var now = clock.GetUtcNow();
        if (user is null || !user.IsActive || user.Email != expected.Email || user.PasswordVersion != expected.PasswordVersion ||
            user.PasswordResetRequestedAt > now.AddMinutes(-1)) return false;
        user.PasswordResetHash = hash;
        user.PasswordResetExpiresAt = now.AddMinutes(15);
        user.PasswordResetRequestedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public Task<User?> FindResetAsync(string hash, CancellationToken ct) => db.Users.AsNoTracking()
        .SingleOrDefaultAsync(x => x.PasswordResetHash == hash && x.IsActive && x.PasswordResetExpiresAt > clock.GetUtcNow(), ct);

    public async Task<bool> ReplaceAsync(User expected, string passwordHash, string? resetHash, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockAsync(expected.Id, ct);
        var now = clock.GetUtcNow();
        if (user is null || !user.IsActive || user.PasswordVersion != expected.PasswordVersion || user.PasswordHash != expected.PasswordHash) return false;
        if (resetHash is not null && (user.PasswordResetHash != resetHash || user.PasswordResetExpiresAt is null || user.PasswordResetExpiresAt <= now)) return false;
        user.PasswordHash = passwordHash;
        user.PasswordVersion++;
        user.PasswordResetHash = null;
        user.PasswordResetExpiresAt = null;
        user.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
