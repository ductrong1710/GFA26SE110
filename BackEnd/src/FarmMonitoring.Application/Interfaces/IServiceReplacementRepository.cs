using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
namespace FarmMonitoring.Application.Interfaces;
public interface IServiceReplacementRepository
{
    Task<PagedResult<ReplacementOption>> OptionsAsync(int jobId, int actorId, PageQuery query, CancellationToken ct);
    Task ReplaceAsync(int jobId, int actorId, ServiceReplacementRequest request, DateTimeOffset now, CancellationToken ct);
}
