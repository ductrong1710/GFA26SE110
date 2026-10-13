using System.Security.Cryptography;
using System.Text;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Authentication;

public sealed class DeviceAuthenticator(AppDbContext db, TimeProvider clock) : IDeviceAuthenticator
{
    public async Task<int?> AuthenticateAsync(string gatewayCode, string apiKey, CancellationToken ct)
    {
        if (gatewayCode.Length is 0 or > 100 || apiKey.Length is < 32 or > 256) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Same lock as credential mutations. A completed revoke/rotate cannot be bypassed
        // by a stale credential read or by updating LastUsedAt on a revoked record.
        var gateway = await db.Gateways.FromSqlInterpolated($"SELECT * FROM gateways WHERE code = {gatewayCode} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (gateway is not { IsActive: true }) return null;
        var credential = await db.GatewayCredentials.SingleOrDefaultAsync(
            x => x.GatewayId == gateway.Id && x.IsActive && x.RevokedAt == null, ct);
        if (credential is null) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(credential.KeyHash))) return null;
        credential.LastUsedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return gateway.Id;
    }
}
