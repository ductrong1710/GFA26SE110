using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Reports;

namespace FarmMonitoring.Application.Interfaces;

public interface IReportRepository
{
    Task<DashboardOverview> DashboardAsync(FarmAccessScope scope, int? farmId, CancellationToken ct);
    Task<PagedResult<SensorReportRow>> SensorsAsync(ReportQuery query, ReportPeriod period, FarmAccessScope scope, CancellationToken ct);
    Task<PagedResult<MissionReportRow>> MissionsAsync(ReportQuery query, ReportPeriod period, FarmAccessScope scope, CancellationToken ct);
    Task<PagedResult<AlertReportRow>> AlertsAsync(ReportQuery query, ReportPeriod period, FarmAccessScope scope, CancellationToken ct);
    Task<PagedResult<DeviceReportRow>> DevicesAsync(DeviceReportQuery query, FarmAccessScope scope, CancellationToken ct);
}
