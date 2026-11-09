using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Features.Alerts;

public sealed class MonitoringSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int GatewayOfflineMinutes { get; set; } = 15;
    public decimal UavLowBatteryPercent { get; set; } = 20;
}
public enum MonitoringResource { SensorChannel, Gateway, Uav }
public sealed record SensorMonitor(SensorThreshold Threshold, DateTimeOffset? LatestValidReadingAt);

public sealed class AlertMonitoringService(IAlertMonitoringRepository monitoring, IAlertRepository repository,
    AlertService alerts, MonitoringSettings settings, TimeProvider clock)
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        foreach (var resource in Enum.GetValues<MonitoringResource>())
        {
            var after = 0;
            while (!ct.IsCancellationRequested)
            {
                var ids = await monitoring.ListIdsAsync(resource, after, ct);
                if (ids.Length == 0) break;
                foreach (var id in ids)
                    await repository.InTransactionAsync(async () =>
                    {
                        var now = clock.GetUtcNow();
                        if (resource == MonitoringResource.SensorChannel)
                        {
                            var monitor = await monitoring.LockSensorAsync(id, ct);
                            if (monitor is not null)
                            {
                                var threshold = monitor.Threshold;
                                var node = threshold.SensorChannel.SensorNode;
                                var latest = monitor.LatestValidReadingAt ?? threshold.SensorChannel.CreatedAt;
                                if (threshold.DataTimeoutMinutes is int timeout && now - latest > TimeSpan.FromMinutes(timeout))
                                    await alerts.RaiseAsync(new(AlertType.SENSOR_DATA_TIMEOUT, AlertSeverity.WARNING, node.Id, id, null, null, null,
                                        "Sensor channel has not reported valid data within its configured timeout.", null), ct);
                                if (threshold.LowBatteryPercent is decimal minimum && node.BatteryPercent < minimum)
                                    await alerts.RaiseAsync(new(AlertType.SENSOR_LOW_BATTERY, AlertSeverity.WARNING, node.Id, null, null, null, null,
                                        "Sensor battery is below its configured threshold.", node.BatteryPercent), ct);
                            }
                        }
                        else if (resource == MonitoringResource.Gateway)
                        {
                            var gateway = await monitoring.LockGatewayAsync(id, ct);
                            if (gateway is { IsActive: true })
                            {
                                if (now - (gateway.LastSeenAt ?? gateway.CreatedAt) > TimeSpan.FromMinutes(settings.GatewayOfflineMinutes))
                                    await alerts.RaiseAsync(new(AlertType.GATEWAY_OFFLINE, AlertSeverity.WARNING, null, null, gateway.Id, null, null,
                                        "Gateway has not contacted the backend within the configured timeout.", null), ct);
                                if (string.Equals(gateway.Status, "ERROR", StringComparison.OrdinalIgnoreCase))
                                    await alerts.RaiseAsync(new(AlertType.GATEWAY_ERROR, AlertSeverity.CRITICAL, null, null, gateway.Id, null, null,
                                        "Gateway reported an error state.", null), ct);
                            }
                        }
                        else
                        {
                            var uav = await monitoring.LockUavAsync(id, ct);
                            if (uav is { IsActive: true } && uav.BatteryPercent < settings.UavLowBatteryPercent)
                                await alerts.RaiseAsync(new(AlertType.UAV_LOW_BATTERY, AlertSeverity.WARNING, null, null, null, uav.Id, null,
                                    "UAV battery is below the configured threshold.", uav.BatteryPercent), ct);
                        }
                        await repository.SaveAsync(ct);
                        return true;
                    }, ct);
                after = ids[^1];
            }
        }
    }
}
