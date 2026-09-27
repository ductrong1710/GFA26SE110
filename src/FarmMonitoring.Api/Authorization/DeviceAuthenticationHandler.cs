using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.Api.Authorization;

public sealed class DeviceAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IDeviceAuthenticator authenticator) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceKey";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var codes = Request.Headers["X-Gateway-Code"];
        var keys = Request.Headers["X-Api-Key"];
        if (codes.Count != 1 || keys.Count != 1) return AuthenticateResult.Fail("Device authentication required.");
        var id = await authenticator.AuthenticateAsync(codes.ToString(), keys.ToString(), Context.RequestAborted);
        if (id is null) return AuthenticateResult.Fail("Invalid device credentials.");
        var identity = new ClaimsIdentity([new Claim("gateway_id", id.Value.ToString(CultureInfo.InvariantCulture))], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(ApiError.Create(Context, "Device authentication required."), Context.RequestAborted);
    }
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(ApiError.Create(Context, "Access denied."), Context.RequestAborted);
    }
}
