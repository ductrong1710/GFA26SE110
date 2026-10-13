using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/gateways/{gatewayId:int}/credential")]
[Authorize(Policy = AccessPolicies.ManageDevices)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiError>(StatusCodes.Status422UnprocessableEntity)]
public sealed class GatewayCredentialsController(GatewayCredentialService credentials) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<GatewayCredentialStatus>>> Status(int gatewayId, CancellationToken ct) =>
        Ok(ApiResponse<GatewayCredentialStatus>.Ok(await credentials.GetStatusAsync(gatewayId, ct)));

    [HttpPost]
    [ProducesResponseType<ApiResponse<IssuedGatewayCredential>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<IssuedGatewayCredential>>> Create(int gatewayId, CancellationToken ct)
    {
        var issued = await credentials.IssueAsync(gatewayId, false, ct);
        return CreatedAtAction(nameof(Status), new { gatewayId }, ApiResponse<IssuedGatewayCredential>.Ok(issued));
    }

    [HttpPost("rotate")]
    public async Task<ActionResult<ApiResponse<IssuedGatewayCredential>>> Rotate(int gatewayId, CancellationToken ct) =>
        Ok(ApiResponse<IssuedGatewayCredential>.Ok(await credentials.IssueAsync(gatewayId, true, ct)));

    [HttpDelete]
    public async Task<IActionResult> Revoke(int gatewayId, CancellationToken ct)
    {
        await credentials.RevokeAsync(gatewayId, ct);
        return NoContent();
    }
}
