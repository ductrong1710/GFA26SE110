using System.Globalization;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class AlertsController(AlertService alerts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<AlertResponse>>> List([FromQuery] AlertQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<AlertResponse>.From(await alerts.ListAsync(query, ct)));
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AlertResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<AlertResponse>.Ok(await alerts.GetAsync(id, ct)));
    [HttpPost("{id:int}/acknowledge")]
    public Task<ActionResult<ApiResponse<AlertResponse>>> Acknowledge(int id, AlertNoteRequest request, CancellationToken ct) => Handle(id, AlertAction.ACKNOWLEDGED, request, ct);
    [HttpPost("{id:int}/notes")]
    public Task<ActionResult<ApiResponse<AlertResponse>>> Note(int id, AlertNoteRequest request, CancellationToken ct) => Handle(id, AlertAction.NOTE_ADDED, request, ct);
    [HttpPost("{id:int}/close")]
    public Task<ActionResult<ApiResponse<AlertResponse>>> Close(int id, AlertNoteRequest request, CancellationToken ct) => Handle(id, AlertAction.CLOSED, request, ct);
    private async Task<ActionResult<ApiResponse<AlertResponse>>> Handle(int id, AlertAction action, AlertNoteRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AlertResponse>.Ok(await alerts.HandleAsync(id, action, request, int.Parse(User.FindFirst("sub")!.Value, CultureInfo.InvariantCulture), ct)));
}
