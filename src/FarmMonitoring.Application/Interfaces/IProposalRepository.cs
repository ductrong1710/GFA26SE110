using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IProposalRepository
{
    Task<PagedResult<ProposalSummary>> ListAsync(int consultationId, PageQuery query, CancellationToken ct);
    Task<DeviceProposal?> GetAsync(int consultationId, int id, CancellationToken ct);
    Task CreateAsync(DeviceProposal proposal, int expectedRevision, CancellationToken ct);
    Task ConfirmAsync(int consultationId, int id, int actorId, bool deposit, string note, DateTimeOffset now, CancellationToken ct);
    Task<InstallationRequest> SubmitAsync(int consultationId, int id, int actorId, DateTimeOffset now, CancellationToken ct);
}
