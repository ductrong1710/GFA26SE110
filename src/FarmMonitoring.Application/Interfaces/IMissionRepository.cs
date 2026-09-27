using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Missions;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IMissionRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task<Mission?> FindAsync(int id, bool forUpdate, CancellationToken ct);
    Task<Farm?> GetFarmAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<SensorNode>> GetSensorsAsync(int[] ids, CancellationToken ct);
    Task<Uav?> LockUavAsync(int id, CancellationToken ct);
    Task<Gateway?> LockGatewayAsync(int id, CancellationToken ct);
    Task<bool> HasOverlapAsync(Mission mission, bool starting, CancellationToken ct);
    Task ReplacePlanAsync(Mission mission, IReadOnlyList<MissionTarget> targets, IReadOnlyList<MissionWaypoint> waypoints, CancellationToken ct);
    void Add(Mission mission);
    void AddLog(MissionLog log);
    Task SaveAsync(CancellationToken ct);
    Task<PagedResult<MissionSummary>> ListAsync(MissionQuery query, CancellationToken ct);
    Task<PagedResult<MissionLogResponse>> LogsAsync(int id, PageQuery query, CancellationToken ct);
}
