using System.Globalization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AuthResponse>.Ok(await auth.LoginAsync(request, ct)));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AuthResponse>.Ok(await auth.RefreshAsync(request, ct)));

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(CurrentUserId(), request, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Logged out."));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<ApiResponse<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Me(CancellationToken ct) =>
        Ok(ApiResponse<UserResponse>.Ok(await auth.GetCurrentUserAsync(CurrentUserId(), ct)));

    private int CurrentUserId() => int.TryParse(User.FindFirst("sub")?.Value, NumberStyles.None,
        CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new AuthException("Authentication required.");
}
