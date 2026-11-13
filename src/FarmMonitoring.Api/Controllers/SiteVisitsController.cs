using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Consultations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/consultations/{id:int}/site-visit")]
[Authorize]
public sealed class SiteVisitsController(SiteVisitService service) : ControllerBase
{
    [HttpPut("/api/consultations/{id:int}/channel")]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    public async Task<ActionResult<ApiResponse<SiteVisitState>>> Channel(int id, SiteVisitChannelRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SiteVisitState>.Ok(await service.ChannelAsync(id, request, ct)));

    [HttpPost("fee-preview")]
    [Authorize(Policy = AccessPolicies.ReadConsultations)]
    public async Task<ActionResult<ApiResponse<SiteVisitFeePreview>>> Preview(int id, SiteVisitPreviewRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SiteVisitFeePreview>.Ok(await service.PreviewAsync(id, request, ct)));

    [HttpGet]
    [Authorize(Policy = AccessPolicies.ReadConsultations)]
    public async Task<ActionResult<ApiResponse<SiteVisitState>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<SiteVisitState>.Ok(await service.GetAsync(id, ct)));

    [HttpPost("proposals")]
    [Authorize(Policy = AccessPolicies.ProcessConsultations)]
    public async Task<ActionResult<ApiResponse<SiteVisitState>>> Propose(int id, SiteVisitProposeRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SiteVisitState>.Ok(await service.ProposeAsync(id, request, ct)));

    [HttpPost("review")]
    [Authorize(Policy = AccessPolicies.ManageUsers)]
    public async Task<ActionResult<ApiResponse<SiteVisitState>>> Review(int id, SiteVisitReviewRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SiteVisitState>.Ok(await service.ReviewAsync(id, request, ct)));

    [HttpPost("payments/confirm")]
    [Authorize(Policy = AccessPolicies.ManageUsers)]
    [RequestSizeLimit(16000000)]
    public async Task<ActionResult<ApiResponse<SiteVisitState>>> Confirm(int id, SiteVisitConfirmRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SiteVisitState>.Ok(await service.ConfirmAsync(id, request, ct)));

    [HttpPost("schedule")]
    [Authorize(Policy = AccessPolicies.ManageUsers)]
    public async Task<ActionResult<ApiResponse<SiteVisitState>>> Schedule(int id, SiteVisitScheduleRequest request, CancellationToken ct) =>
        Ok(ApiResponse<SiteVisitState>.Ok(await service.ScheduleAsync(id, request, ct)));
}
