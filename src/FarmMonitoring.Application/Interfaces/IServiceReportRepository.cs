using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;
public interface IServiceReportRepository
{
    Task<PagedResult<ServiceReportSummary>> ListAsync(int jobId, PageQuery query, CancellationToken ct);
    Task<ServiceReport?> GetAsync(int jobId, int id, CancellationToken ct);
    Task<ServiceReport> SubmitAsync(int jobId, int actorId, ServiceReportRequest request, DateTimeOffset now, CancellationToken ct);
    Task DecideAsync(int jobId, int actorId, string action, ServiceReportDecisionRequest request, DateTimeOffset now, CancellationToken ct);
}
