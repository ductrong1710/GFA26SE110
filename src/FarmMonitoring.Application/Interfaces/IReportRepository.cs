using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Reports;

namespace FarmMonitoring.Application.Interfaces;

public interface IReportRepository
{
    Task<DashboardOverview> DashboardAsync(CancellationToken ct);
    Task<PagedResult<SensorReportRow>> SensorsAsync(ReportQuery query, ReportPeriod period, CancellationToken ct);
    Task<PagedResult<MissionReportRow>> MissionsAsync(ReportQuery query, ReportPeriod period, CancellationToken ct);
    Task<PagedResult<AlertReportRow>> AlertsAsync(ReportQuery query, ReportPeriod period, bool operationalOnly, CancellationToken ct);
    Task<PagedResult<DeviceReportRow>> DevicesAsync(DeviceReportQuery query, CancellationToken ct);
}
