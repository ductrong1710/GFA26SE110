using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;

namespace FarmMonitoring.Application.Interfaces;

public interface IInstallationDossierRepository
{
    Task<PagedResult<DossierSummary>> ListAsync(int actor, PageQuery query, CancellationToken ct);
    Task<DossierDecision> SubmitAsync(int id, int actor, int revision, string note, EvidenceImageData[] bills, DossierCustomerRequest? customer, DateTimeOffset now, CancellationToken ct);
    Task<DossierDecision> ReviewAsync(int id, int actor, DossierReviewRequest request, DateTimeOffset now, CancellationToken ct);
}
