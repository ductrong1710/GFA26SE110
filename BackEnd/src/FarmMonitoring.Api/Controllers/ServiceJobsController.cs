using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;
[ApiController]
[Route("api/service-jobs")]
[Authorize(Policy = AccessPolicies.OperationalUser)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ServiceJobsController(ServiceJobService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<ServiceJobSummary>>> List([FromQuery] PageQuery query, CancellationToken ct) => Ok(ApiPageResponse<ServiceJobSummary>.From(await service.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ServiceJobDetail>>> Get(int id, CancellationToken ct) => Ok(ApiResponse<ServiceJobDetail>.Ok(await service.GetAsync(id, ct)));
    [HttpPost("/api/device-incidents/{id:int}/jobs")]
    public async Task<ActionResult<ApiResponse<ServiceJobDetail>>> Create(int id, ServiceJobAssignmentRequest request, CancellationToken ct)
    {
        var job = await service.AssignAsync(id, true, request, ct);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, ApiResponse<ServiceJobDetail>.Ok(job));
    }
    [HttpPost("{id:int}/assignment")]
    public async Task<ActionResult<ApiResponse<ServiceJobDetail>>> Assign(int id, ServiceJobAssignmentRequest request, CancellationToken ct) => Ok(ApiResponse<ServiceJobDetail>.Ok(await service.AssignAsync(id, false, request, ct)));
    [HttpPost("{id:int}/start")]
    public async Task<ActionResult<ApiResponse<ServiceJobDetail>>> Start(int id, ServiceJobStartRequest request, CancellationToken ct) => Ok(ApiResponse<ServiceJobDetail>.Ok(await service.StartAsync(id, request, ct)));
}
