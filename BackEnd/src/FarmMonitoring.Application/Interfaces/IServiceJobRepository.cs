using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;

namespace FarmMonitoring.Application.Interfaces;
public interface IServiceJobRepository
{
    Task<PagedResult<ServiceJobSummary>> ListAsync(int actorId, PageQuery query, CancellationToken ct);
    Task<ServiceJobDetail> GetAsync(int id, int actorId, CancellationToken ct);
    Task<int> AssignAsync(int id, bool create, int actorId, ServiceJobAssignmentRequest request, DateTimeOffset now, CancellationToken ct);
    Task StartAsync(int id, int actorId, int expectedRevision, DateTimeOffset now, CancellationToken ct);
}
