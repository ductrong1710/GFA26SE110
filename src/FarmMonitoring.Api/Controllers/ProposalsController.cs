using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/consultations/{consultationId:int}/proposals")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
public sealed class ProposalsController(ProposalService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<ProposalSummary>>> List(int consultationId, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<ProposalSummary>.From(await service.ListAsync(consultationId, query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProposalResponse>>> Get(int consultationId, int id, CancellationToken ct) =>
        Ok(ApiResponse<ProposalResponse>.Ok(await service.GetAsync(consultationId, id, ct)));
    [HttpPost]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    [RequestSizeLimit(524288)]
    public async Task<ActionResult<ApiResponse<ProposalResponse>>> Create(int consultationId, ProposalRequest request, CancellationToken ct)
    {
        var row = await service.CreateAsync(consultationId, request, ct);
        return CreatedAtAction(nameof(Get), new { consultationId, id = row.Id }, ApiResponse<ProposalResponse>.Ok(row));
    }
    [HttpPost("{id:int}/confirm-plan")]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    public async Task<ActionResult<ApiResponse<ProposalResponse>>> Plan(int consultationId, int id, ProposalConfirmationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ProposalResponse>.Ok(await service.ConfirmAsync(consultationId, id, false, request, ct)));
    [HttpPost("{id:int}/confirm-deposit")]
    [Authorize(Policy = AccessPolicies.ReadConsultations)]
    public async Task<ActionResult<ApiResponse<ProposalResponse>>> Deposit(int consultationId, int id, ProposalConfirmationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ProposalResponse>.Ok(await service.ConfirmAsync(consultationId, id, true, request, ct)));
    [HttpPost("{id:int}/submit-installation")]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    public async Task<ActionResult<ApiResponse<InstallationRequestResponse>>> Submit(int consultationId, int id, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<InstallationRequestResponse>.Ok(await service.SubmitAsync(consultationId, id, ct)));
}
