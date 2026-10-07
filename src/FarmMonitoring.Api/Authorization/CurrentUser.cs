using System.Globalization;
using FarmMonitoring.Application.Interfaces;

namespace FarmMonitoring.Api.Authorization;

public sealed class CurrentUser(IHttpContextAccessor context) : ICurrentUser
{
    public int? UserId => context.HttpContext?.User.Identity?.IsAuthenticated == true &&
        int.TryParse(context.HttpContext.User.FindFirst("sub")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}
