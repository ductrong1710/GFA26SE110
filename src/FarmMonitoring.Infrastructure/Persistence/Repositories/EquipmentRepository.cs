using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Equipment;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class EquipmentRepository(AppDbContext db) : IEquipmentRepository
{
    public async Task<PagedResult<UavResponse>> ListUavsAsync(PageQuery query, CancellationToken ct)
    {
        var rows = db.Uavs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new UavResponse(x.Id, x.Code, x.Name, x.Model, x.Status, x.BatteryPercent, x.LastSeenAt,
                x.IsActive, x.CreatedAt, x.UpdatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<GatewayResponse>> ListGatewaysAsync(PageQuery query, CancellationToken ct)
    {
        var rows = db.Gateways.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new GatewayResponse(x.Id, x.Code, x.Name, x.GatewayType, x.Status, x.UavId, x.BatteryPercent,
                x.LastSeenAt, x.FirmwareVersion, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public Task<Uav?> FindUavAsync(int id, CancellationToken ct) => db.Uavs.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Gateway?> FindGatewayAsync(int id, CancellationToken ct) => db.Gateways.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddUav(Uav uav) => db.Uavs.Add(uav);
    public void AddGateway(Gateway gateway) => db.Gateways.Add(gateway);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            && pg.ConstraintName is "IX_uavs_code" or "IX_gateways_code")
        { throw new ConflictException("The equipment code is already registered."); }
    }
}
