using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;

namespace FarmMonitoring.Application.Interfaces;

public interface IInstallationJobRepository
{
    Task<PagedResult<JobSummary>> ListAsync(int actorId, PageQuery query, CancellationToken ct);
    Task<PagedResult<TechnicianOption>> TechniciansAsync(int actorId, PageQuery query, CancellationToken ct);
    Task<JobDetail> GetAsync(int id, int actorId, CancellationToken ct);
    Task<int> AssignAsync(int id, bool create, int actorId, JobAssignmentRequest request, DateTimeOffset now, CancellationToken ct);
    Task StartAsync(int id, int actorId, int expectedRevision, DateTimeOffset now, CancellationToken ct);
}
