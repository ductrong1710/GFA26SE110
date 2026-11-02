using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;
[ApiController]
[Route("api/device-incidents")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DeviceIncidentsController(DeviceIncidentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<DeviceIncidentSummary>>> List([FromQuery] PageQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<DeviceIncidentSummary>.From(await service.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<DeviceIncidentDetail>>> Get(int id, CancellationToken ct)
        => Ok(ApiResponse<DeviceIncidentDetail>.Ok(await service.GetAsync(id, ct)));
    [HttpPost]
    [Authorize(Policy = AccessPolicies.SubmitDeviceIncidents)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<DeviceIncidentDetail>>> Create(CreateDeviceIncidentRequest request, CancellationToken ct)
    {
        var row = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id }, ApiResponse<DeviceIncidentDetail>.Ok(row));
    }
}
