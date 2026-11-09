using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AccessPolicies.ManageUsers)]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
public sealed class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiPageResponse<ManagedUserResponse>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(ApiPageResponse<ManagedUserResponse>.From(await users.ListAsync(query, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ManagedUserResponse>>> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<ManagedUserResponse>.Ok(await users.GetAsync(id, ct)));

    [HttpPost]
    [ProducesResponseType<ApiResponse<ManagedUserResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ManagedUserResponse>>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, ApiResponse<ManagedUserResponse>.Ok(user));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ManagedUserResponse>>> Update(int id, UpdateUserRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedUserResponse>.Ok(await users.UpdateAsync(id, request, ct)));

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<ManagedUserResponse>>> Status(int id, UserStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedUserResponse>.Ok(await users.SetStatusAsync(id, request, ct)));

    [HttpPut("{id:int}/roles")]
    public async Task<ActionResult<ApiResponse<ManagedUserResponse>>> Roles(int id, UserRolesRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ManagedUserResponse>.Ok(await users.SetRolesAsync(id, request, ct)));
}
