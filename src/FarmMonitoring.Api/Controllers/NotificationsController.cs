using System.Globalization;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Alerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = AccessPolicies.ReadFarmData)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
public sealed class NotificationsController(AlertService alerts) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirst("sub")!.Value, CultureInfo.InvariantCulture);
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<NotificationResponse>>> List([FromQuery] NotificationQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<NotificationResponse>.From(await alerts.NotificationsAsync(Actor, query, ct)));
    [HttpPatch("{id:int}/read")]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> Read(int id, CancellationToken ct) =>
        Ok(ApiResponse<NotificationResponse>.Ok(await alerts.ReadAsync(Actor, id, ct)));
}
