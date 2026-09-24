using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class FarmRepository(AppDbContext db) : IFarmRepository
{
    public async Task<PagedResult<FarmResponse>> ListFarmsAsync(PageQuery query, CancellationToken ct)
    {
        var rows = db.Farms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Name.ToLower().Contains(term));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new FarmResponse(x.Id, x.Name, x.Description, x.Latitude, x.Longitude, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<PagedResult<ZoneResponse>> ListZonesAsync(int farmId, PageQuery query, CancellationToken ct)
    {
        var rows = db.Zones.AsNoTracking().Where(x => x.FarmId == farmId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Name.ToLower().Contains(term));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ZoneResponse(x.Id, x.FarmId, x.Name, x.Description, x.CenterLatitude, x.CenterLongitude, x.CreatedAt, x.UpdatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }

    public Task<Farm?> FindFarmAsync(int id, CancellationToken ct) => db.Farms.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Zone?> FindZoneAsync(int id, CancellationToken ct) => db.Zones.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddFarm(Farm farm) => db.Farms.Add(farm);
    public void AddZone(Zone zone) => db.Zones.Add(zone);

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_zones_farm_id_name" })
        { throw new ConflictException("Zone name is already in use in this farm."); }
    }
}
