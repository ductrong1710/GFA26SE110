using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/installation-dossiers")]
[Authorize]
public sealed class InstallationDossiersController(InstallationDossierService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiPageResponse<DossierSummary>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<DossierSummary>.From(await service.ListAsync(query, ct)));
    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    [RequestSizeLimit(16000000)]
    public async Task<ActionResult<ApiResponse<DossierDecision>>> Submit(int id, DossierSubmissionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DossierDecision>.Ok(await service.SubmitAsync(id, request, ct)));
    [HttpPost("{id:int}/review")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<DossierDecision>>> Review(int id, DossierReviewRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DossierDecision>.Ok(await service.ReviewAsync(id, request, ct)));
}
