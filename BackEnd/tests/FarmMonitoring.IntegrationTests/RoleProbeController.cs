using FarmMonitoring.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.IntegrationTests;

// Test-only endpoint verifies production role mapping; never included in the API assembly.
[ApiController]
[Route("test/admin")]
[Authorize(Roles = RoleNames.FarmAdministrator)]
public sealed class RoleProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
