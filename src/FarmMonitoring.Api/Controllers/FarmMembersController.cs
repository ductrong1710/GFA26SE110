using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:int}/members")]
[Authorize(Policy = AccessPolicies.ManageUsers)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(400)]
[ProducesResponseType<ApiError>(401)]
[ProducesResponseType<ApiError>(403)]
[ProducesResponseType<ApiError>(404)]
[ProducesResponseType<ApiError>(409)]
[ProducesResponseType<ApiError>(422)]
public sealed class FarmMembersController(FarmMembershipService members) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<FarmMemberResponse>>> List(int farmId, [FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<FarmMemberResponse>.From(await members.ListAsync(farmId, query, ct)));

    [HttpPost]
    [ProducesResponseType<ApiResponse<FarmMemberResponse>>(201)]
    public async Task<ActionResult<ApiResponse<FarmMemberResponse>>> Assign(int farmId, FarmMemberRequest request, CancellationToken ct) =>
        CreatedAtAction(nameof(List), new { farmId }, ApiResponse<FarmMemberResponse>.Ok(await members.AssignAsync(farmId, request, ct)));

    [HttpDelete("{userId:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Remove(int farmId, int userId, CancellationToken ct)
    {
        await members.RemoveAsync(farmId, userId, ct);
        return NoContent();
    }
}
