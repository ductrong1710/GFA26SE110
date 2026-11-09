using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IInstallationRepository
{
    Task<PagedResult<InstallationSummary>> ListAsync(int actorId, bool admin, bool engineer, PageQuery query, CancellationToken ct);
    Task<InstallationRequest?> GetAsync(int id, CancellationToken ct);
    Task ReviewAsync(int id, int actorId, bool approve, string note, DateTimeOffset now, CancellationToken ct);
}
