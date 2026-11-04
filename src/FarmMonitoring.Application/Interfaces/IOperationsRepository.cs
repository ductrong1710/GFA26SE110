using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;

namespace FarmMonitoring.Application.Interfaces;
public interface IOperationsRepository
{
    Task<object> CalendarAsync(int actor, DateTimeOffset? from, DateTimeOffset? to, int? technicianId, CancellationToken ct);
    Task<object> AvailableAsync(int actor, DateTimeOffset from, DateTimeOffset to, string? excludeType, int? excludeId, CancellationToken ct);
    Task<object> DashboardAsync(int actor, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<object> LeadsAsync(int actor, DateTimeOffset? from, DateTimeOffset? to, PageQuery query, CancellationToken ct);
    Task<object> DeviceHistoryAsync(int actor, string code, PageQuery query, CancellationToken ct);
    Task<object> InstalledAsync(int actor, string jobType, int jobId, PageQuery query, CancellationToken ct);
    Task<object> PlansAsync(int actor, int jobId, CancellationToken ct);
    Task<object> ProposeAsync(int actor, int jobId, ProposeServicePlan request, CancellationToken ct);
    Task<object> ReviewAsync(int actor, int planId, ReviewServicePlan request, CancellationToken ct);
    Task<object> ExecuteAsync(int actor, int planId, ExecuteServicePlan request, CancellationToken ct);
}
