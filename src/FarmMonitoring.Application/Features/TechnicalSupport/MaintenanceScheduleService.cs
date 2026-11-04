using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.TechnicalSupport;
public sealed record MaintenanceScheduleResponse(int Id, int? RentalDeviceAssignmentId, string? DeviceType, string Policy,
    int IntervalDays, DateTimeOffset NextDueAt, int TechnicianId, bool Enabled, int Revision,
    int CreatedById, int UpdatedById, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int? FarmId = null)
{
    public static MaintenanceScheduleResponse From(MaintenanceSchedule x) => new(x.Id, x.RentalDeviceAssignmentId, x.DeviceType,
        x.Policy, x.IntervalDays, x.NextDueAt, x.TechnicianId, x.Enabled, x.Revision, x.CreatedById, x.UpdatedById, x.CreatedAt, x.UpdatedAt, x.FarmId);
}
public sealed record DisableMaintenanceScheduleRequest(int ExpectedRevision);
public sealed record MaintenanceOccurrenceResponse(int Id, int JobId, int RentalDeviceAssignmentId, string DeviceCode,
    DateTimeOffset DueAt, DateTimeOffset CreatedAt, string Policy, string Status);
public sealed class MaintenanceScheduleService(IMaintenanceScheduleRepository repository, ICurrentUser current, TimeProvider clock,
    IValidator<PageQuery> pages, IValidator<MaintenanceScheduleRequest> schedules)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<MaintenanceOccurrenceResponse>> OccurrencesAsync(int id, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.OccurrencesAsync(id, Actor, query, ct);
    }
    public async Task<PagedResult<MaintenanceScheduleResponse>> ListAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(Actor, query, ct);
    }
    public Task<MaintenanceScheduleResponse> GetAsync(int id, CancellationToken ct) => repository.GetAsync(id, Actor, ct);
    public async Task<MaintenanceScheduleResponse> SaveAsync(int? id, MaintenanceScheduleRequest request, CancellationToken ct)
    {
        await schedules.ValidateAndThrowAsync(request, ct);
        return await repository.SaveAsync(id, Actor, request, clock.GetUtcNow(), ct);
    }
    public Task<MaintenanceScheduleResponse> DisableAsync(int id, DisableMaintenanceScheduleRequest request, CancellationToken ct)
    {
        if (request.ExpectedRevision is <= 0 or int.MaxValue) throw new ValidationException("A valid expectedRevision is required.");
        return repository.DisableAsync(id, Actor, request.ExpectedRevision, clock.GetUtcNow(), ct);
    }
}
