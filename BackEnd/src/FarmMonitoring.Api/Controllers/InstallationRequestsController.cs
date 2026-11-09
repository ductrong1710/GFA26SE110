using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/installation-requests")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
public sealed class InstallationRequestsController(InstallationReviewService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<InstallationSummary>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<InstallationSummary>.From(await service.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<InstallationDetail>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<InstallationDetail>.Ok(await service.GetAsync(id, ct)));
    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<InstallationDetail>>> Approve(int id, InstallationReviewRequest request, CancellationToken ct) =>
        Ok(ApiResponse<InstallationDetail>.Ok(await service.ReviewAsync(id, true, request, ct)));
    [HttpPost("{id:int}/return")]
    [Authorize(Policy = AccessPolicies.ReviewInstallations)]
    public async Task<ActionResult<ApiResponse<InstallationDetail>>> Return(int id, InstallationReviewRequest request, CancellationToken ct) =>
        Ok(ApiResponse<InstallationDetail>.Ok(await service.ReviewAsync(id, false, request, ct)));
}
