using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IAlertMonitoringRepository
{
    Task<int[]> ListIdsAsync(MonitoringResource resource, int after, CancellationToken ct);
    Task<SensorMonitor?> LockSensorAsync(int channelId, CancellationToken ct);
    Task<Gateway?> LockGatewayAsync(int id, CancellationToken ct);
    Task<Uav?> LockUavAsync(int id, CancellationToken ct);
}
