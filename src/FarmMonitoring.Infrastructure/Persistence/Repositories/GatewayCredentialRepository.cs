using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Equipment;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class GatewayCredentialRepository(AppDbContext db) : IGatewayCredentialRepository
{
    public async Task<GatewayCredentialStatus> GetStatusAsync(int gatewayId, CancellationToken ct)
    {
        var gateway = await db.Gateways.AsNoTracking().SingleOrDefaultAsync(x => x.Id == gatewayId, ct)
            ?? throw new NotFoundException("Gateway not found.");
        // Project metadata only: the hash never leaves persistence through this query.
        return await db.GatewayCredentials.AsNoTracking().Where(x => x.GatewayId == gatewayId)
            .OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Id)
            .Select(x => new GatewayCredentialStatus(gatewayId, gateway.Code, true,
                gateway.IsActive && x.IsActive && x.RevokedAt == null, x.CreatedAt, x.LastUsedAt, x.RevokedAt, x.KeyPrefix))
            .FirstOrDefaultAsync(ct) ?? new(gatewayId, gateway.Code, false, false, null, null, null, null);
    }

    public async Task<string> IssueAsync(int gatewayId, GatewayCredential credential, bool rotate, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var gateway = await LockGatewayAsync(gatewayId, ct);
        if (!gateway.IsActive) throw new BusinessRuleException("Gateway must be active to issue a credential.");
        var active = await db.GatewayCredentials.SingleOrDefaultAsync(x => x.GatewayId == gatewayId && x.IsActive, ct);
        if (rotate && active is null) throw new ConflictException("No active credential to rotate. Create a credential first.");
        if (!rotate && active is not null) throw new ConflictException("Gateway already has an active credential. Rotate it instead.");
        if (active is not null)
        {
            active.IsActive = false;
            active.RevokedAt = credential.CreatedAt;
            // Free the partial unique index before inserting its replacement, within the same transaction.
            await db.SaveChangesAsync(ct);
        }
        db.GatewayCredentials.Add(credential);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return gateway.Code;
    }

    public async Task RevokeAsync(int gatewayId, DateTimeOffset revokedAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockGatewayAsync(gatewayId, ct);
        await db.GatewayCredentials.Where(x => x.GatewayId == gatewayId && x.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false).SetProperty(x => x.RevokedAt, revokedAt), ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Gateway> LockGatewayAsync(int id, CancellationToken ct) =>
        await db.Gateways.FromSqlInterpolated($"SELECT * FROM gateways WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Gateway not found.");
}
