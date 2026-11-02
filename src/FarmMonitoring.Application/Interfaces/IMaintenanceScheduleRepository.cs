using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
namespace FarmMonitoring.Application.Interfaces;
public interface IMaintenanceScheduleRepository
{
    Task<int[]> DueIdsAsync(int afterId, DateTimeOffset now, CancellationToken ct);
    Task<int> ProcessDueAsync(int id, DateTimeOffset now, CancellationToken ct);
    Task<PagedResult<MaintenanceOccurrenceResponse>> OccurrencesAsync(int id, int actorId, PageQuery query, CancellationToken ct);
    Task<PagedResult<MaintenanceScheduleResponse>> ListAsync(int actorId, PageQuery query, CancellationToken ct);
    Task<MaintenanceScheduleResponse> GetAsync(int id, int actorId, CancellationToken ct);
    Task<MaintenanceScheduleResponse> SaveAsync(int? id, int actorId, MaintenanceScheduleRequest request, DateTimeOffset now, CancellationToken ct);
    Task<MaintenanceScheduleResponse> DisableAsync(int id, int actorId, int expectedRevision, DateTimeOffset now, CancellationToken ct);
}
