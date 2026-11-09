using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FarmMonitoring.Api.Controllers;
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/service-jobs/{jobId:int}")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
public sealed class ServiceReplacementsController(ServiceReplacementService service) : ControllerBase
{
    [HttpGet("replacement-options")]
    public async Task<ActionResult<ApiPageResponse<ReplacementOption>>> Options(int jobId, [FromQuery] PageQuery query, CancellationToken ct) => Ok(ApiPageResponse<ReplacementOption>.From(await service.OptionsAsync(jobId, query, ct)));
    [HttpPost("replacements")]
    public async Task<ActionResult<ApiResponse<ServiceJobDetail>>> Replace(int jobId, ServiceReplacementRequest request, CancellationToken ct) => Ok(ApiResponse<ServiceJobDetail>.Ok(await service.ReplaceAsync(jobId, request, ct)));
}
