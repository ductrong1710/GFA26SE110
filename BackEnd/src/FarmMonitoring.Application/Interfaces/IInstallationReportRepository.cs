using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;
public interface IInstallationReportRepository
{
    Task<PagedResult<ReportSummary>> ListAsync(int jobId, PageQuery query, CancellationToken ct);
    Task<InstallationReport?> GetAsync(int jobId, int id, CancellationToken ct);
    Task<InstallationReport> SubmitAsync(int jobId, int actorId, InstallationReportRequest request, DateTimeOffset now, CancellationToken ct);
    Task DecideAsync(int jobId, int actorId, string action, ReportDecisionRequest request, DateTimeOffset now, CancellationToken ct);
}
