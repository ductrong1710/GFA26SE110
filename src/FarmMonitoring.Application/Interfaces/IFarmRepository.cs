using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IFarmRepository
{
    Task<PagedResult<FarmResponse>> ListFarmsAsync(PageQuery query, CancellationToken ct);
    Task<PagedResult<ZoneResponse>> ListZonesAsync(int farmId, PageQuery query, CancellationToken ct);
    Task<Farm?> FindFarmAsync(int id, CancellationToken ct);
    Task<Zone?> FindZoneAsync(int id, CancellationToken ct);
    void AddFarm(Farm farm);
    void AddZone(Zone zone);
    Task SaveAsync(CancellationToken ct);
}
