using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Reports;

public sealed class ReportService(IReportRepository repository, IAuthRepository users, IValidator<PageQuery> pages, TimeProvider clock)
{
    public Task<DashboardOverview> DashboardAsync(CancellationToken ct) => repository.DashboardAsync(ct);
    public async Task<PagedResult<SensorReportRow>> SensorsAsync(ReportQuery query, CancellationToken ct) =>
        await repository.SensorsAsync(query, await Period(query, ct), ct);
    public async Task<PagedResult<MissionReportRow>> MissionsAsync(ReportQuery query, CancellationToken ct)
    {
        if (query.ZoneId.HasValue || query.SensorTypeId.HasValue) throw new ValidationException("Mission reports support farm and date filters.");
        return await repository.MissionsAsync(query, await Period(query, ct), ct);
    }
    public async Task<PagedResult<AlertReportRow>> AlertsAsync(int actor, ReportQuery query, CancellationToken ct)
    {
        if (query.SensorTypeId.HasValue) throw new ValidationException("Alert reports do not support sensor type filters.");
        var period = await Period(query, ct);
        var user = await users.GetUserByIdAsync(actor, ct) ?? throw new AccessDeniedException("User unavailable.");
        return await repository.AlertsAsync(query, period, !user.UserRoles.Any(x => x.Role.Name == RoleNames.FarmAdministrator), ct);
    }
    public async Task<PagedResult<DeviceReportRow>> DevicesAsync(DeviceReportQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        if (query.DeviceType is not (null or "SENSOR_NODE" or "UAV" or "GATEWAY")) throw new ValidationException("Invalid device type.");
        return await repository.DevicesAsync(query, ct);
    }
    private async Task<ReportPeriod> Period(ReportQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        if (query.FarmId <= 0 || query.ZoneId <= 0 || query.SensorTypeId <= 0) throw new ValidationException("Filter IDs must be positive.");
        var to = (query.To ?? clock.GetUtcNow()).ToUniversalTime();
        var from = (query.From ?? (to >= DateTimeOffset.MinValue.AddDays(30) ? to.AddDays(-30) : DateTimeOffset.MinValue)).ToUniversalTime();
        if (from > to || to - from > TimeSpan.FromDays(366)) throw new ValidationException("Report range must be chronological and at most 366 days.");
        return new(from, to);
    }
}
