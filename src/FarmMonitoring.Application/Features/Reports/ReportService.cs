using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Reports;

public sealed class ReportService(FarmAccessService access, IReportRepository repository, IValidator<PageQuery> pages, TimeProvider clock)
{
    public async Task<DashboardOverview> DashboardAsync(int? farmId, CancellationToken ct)
    {
        await access.CheckFilterAsync(FarmResource.Farm, farmId, ct);
        return await repository.DashboardAsync(await access.GetScopeAsync(ct), farmId, ct);
    }
    public async Task<PagedResult<SensorReportRow>> SensorsAsync(ReportQuery query, CancellationToken ct) =>
        await repository.SensorsAsync(query, await Period(query, ct), await access.GetScopeAsync(ct), ct);
    public async Task<PagedResult<MissionReportRow>> MissionsAsync(ReportQuery query, CancellationToken ct)
    {
        if (query.ZoneId.HasValue || query.SensorTypeId.HasValue) throw new ValidationException("Mission reports support farm and date filters.");
        return await repository.MissionsAsync(query, await Period(query, ct), await access.GetScopeAsync(ct), ct);
    }
    public async Task<PagedResult<AlertReportRow>> AlertsAsync(ReportQuery query, CancellationToken ct)
    {
        if (query.SensorTypeId.HasValue) throw new ValidationException("Alert reports do not support sensor type filters.");
        var period = await Period(query, ct);
        return await repository.AlertsAsync(query, period, await access.GetScopeAsync(ct), ct);
    }
    public async Task<PagedResult<DeviceReportRow>> DevicesAsync(DeviceReportQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        if (query.DeviceType is not (null or "SENSOR_NODE" or "UAV" or "GATEWAY")) throw new ValidationException("Invalid device type.");
        await access.CheckFilterAsync(FarmResource.Farm, query.FarmId, ct);
        return await repository.DevicesAsync(query, await access.GetScopeAsync(ct), ct);
    }
    private async Task<ReportPeriod> Period(ReportQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        if (query.FarmId <= 0 || query.ZoneId <= 0 || query.SensorTypeId <= 0) throw new ValidationException("Filter IDs must be positive.");
        await access.CheckFilterAsync(FarmResource.Farm, query.FarmId, ct);
        await access.CheckFilterAsync(FarmResource.Zone, query.ZoneId, ct);
        var to = (query.To ?? clock.GetUtcNow()).ToUniversalTime();
        var from = (query.From ?? (to >= DateTimeOffset.MinValue.AddDays(30) ? to.AddDays(-30) : DateTimeOffset.MinValue)).ToUniversalTime();
        if (from > to || to - from > TimeSpan.FromDays(366)) throw new ValidationException("Report range must be chronological and at most 366 days.");
        return new(from, to);
    }
}
