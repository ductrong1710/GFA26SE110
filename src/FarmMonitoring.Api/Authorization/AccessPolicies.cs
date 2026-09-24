using System.Globalization;
using FarmMonitoring.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace FarmMonitoring.Api.Authorization;

public static class AccessPolicies
{
    public const string ManageUsers = nameof(ManageUsers);
    public const string ReadFarmData = nameof(ReadFarmData);
    public const string ManageFarms = nameof(ManageFarms);
    public const string ManageDevices = nameof(ManageDevices);
}

public sealed record CurrentRoleRequirement(params string[] Roles) : IAuthorizationRequirement;

public sealed class CurrentRoleHandler(IAuthRepository users) : AuthorizationHandler<CurrentRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CurrentRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !int.TryParse(context.User.FindFirst("sub")?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0) return;
        var ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var user = await users.GetUserByIdAsync(id, ct);
        if (user is { IsActive: true } && user.UserRoles.Any(x => requirement.Roles.Contains(x.Role.Name, StringComparer.Ordinal)))
            context.Succeed(requirement);
    }
}
