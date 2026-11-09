using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;
[ApiController]
[Route("api/maintenance-schedules")]
[Authorize(Policy = AccessPolicies.ManageMaintenanceSchedules)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MaintenanceSchedulesController(MaintenanceScheduleService service) : ControllerBase
{
    [HttpGet("{id:int}/occurrences")]
    public async Task<ActionResult<ApiPageResponse<MaintenanceOccurrenceResponse>>> Occurrences(int id, [FromQuery] PageQuery query, CancellationToken ct)
        => Ok(ApiPageResponse<MaintenanceOccurrenceResponse>.From(await service.OccurrencesAsync(id, query, ct)));
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<MaintenanceScheduleResponse>>> List([FromQuery] PageQuery query, CancellationToken ct) => Ok(ApiPageResponse<MaintenanceScheduleResponse>.From(await service.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<MaintenanceScheduleResponse>>> Get(int id, CancellationToken ct) => Ok(ApiResponse<MaintenanceScheduleResponse>.Ok(await service.GetAsync(id, ct)));
    [HttpPost]
    public async Task<ActionResult<ApiResponse<MaintenanceScheduleResponse>>> Create(MaintenanceScheduleRequest request, CancellationToken ct)
    {
        var row = await service.SaveAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id }, ApiResponse<MaintenanceScheduleResponse>.Ok(row));
    }
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<MaintenanceScheduleResponse>>> Update(int id, MaintenanceScheduleRequest request, CancellationToken ct) => Ok(ApiResponse<MaintenanceScheduleResponse>.Ok(await service.SaveAsync(id, request, ct)));
    [HttpPost("{id:int}/disable")]
    public async Task<ActionResult<ApiResponse<MaintenanceScheduleResponse>>> Disable(int id, DisableMaintenanceScheduleRequest request, CancellationToken ct) => Ok(ApiResponse<MaintenanceScheduleResponse>.Ok(await service.DisableAsync(id, request, ct)));
}
