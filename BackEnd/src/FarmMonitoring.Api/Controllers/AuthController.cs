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
public sealed class AuthController(AuthService auth, PasswordRecoveryService passwords) : ControllerBase
{
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await passwords.ForgotAsync(request, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "If the account is eligible, password reset instructions will be sent."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await passwords.ResetAsync(request, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Password reset. Sign in again."));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await passwords.ChangeAsync(CurrentUserId(), request, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Password changed. Sign in again."));
    }

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
