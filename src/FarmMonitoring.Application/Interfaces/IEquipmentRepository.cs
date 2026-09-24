using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Equipment;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IEquipmentRepository
{
    Task<PagedResult<UavResponse>> ListUavsAsync(PageQuery query, CancellationToken ct);
    Task<PagedResult<GatewayResponse>> ListGatewaysAsync(PageQuery query, CancellationToken ct);
    Task<Uav?> FindUavAsync(int id, CancellationToken ct);
    Task<Gateway?> FindGatewayAsync(int id, CancellationToken ct);
    void AddUav(Uav uav);
    void AddGateway(Gateway gateway);
    Task SaveAsync(CancellationToken ct);
}
