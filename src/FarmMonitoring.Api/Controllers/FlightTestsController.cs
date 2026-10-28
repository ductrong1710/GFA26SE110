using System.Globalization;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.FlightTests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[Route("api/gateways/{gatewayId:int}/flight-tests")]
[Authorize(Policy = AccessPolicies.ManageUsers)]
public sealed class FlightTestsController(FlightTestService tests) : ControllerBase
{
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<FlightTestResponse>>> Cancel(int gatewayId, int id, CancelFlightTestRequest request, CancellationToken ct) =>
        Ok(ApiResponse<FlightTestResponse>.Ok(await tests.CancelAsync(gatewayId, id, request, ct)));
    [HttpPost]
    public async Task<ActionResult<ApiResponse<FlightTestResponse>>> Create(int gatewayId, CreateFlightTestRequest request, CancellationToken ct)
    {
        var result = await tests.CreateAsync(gatewayId, request, ct);
        return AcceptedAtAction(nameof(Get), new {gatewayId, id = result.Id}, ApiResponse<FlightTestResponse>.Ok(result));
    }
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<FlightTestResponse>>> Get(int gatewayId, int id, CancellationToken ct) =>
        Ok(ApiResponse<FlightTestResponse>.Ok(await tests.GetAsync(gatewayId, id, ct)));
}

[ApiController]
[Route("api/device/gateways/{gatewayId:int}/flight-tests")]
[Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
public sealed class DeviceFlightTestsController(FlightTestService tests) : ControllerBase
{
    private int Gateway => int.Parse(User.FindFirst("gateway_id")!.Value, CultureInfo.InvariantCulture);
    [HttpGet("next")]
    public async Task<ActionResult<ApiResponse<FlightTestResponse?>>> Next(int gatewayId, CancellationToken ct) =>
        Ok(ApiResponse<FlightTestResponse?>.Ok(await tests.NextAsync(gatewayId, Gateway, ct)));
    [HttpPost("{id:int}/claim")]
    public async Task<ActionResult<ApiResponse<FlightTestResponse>>> Claim(int gatewayId, int id, CancellationToken ct) =>
        Ok(ApiResponse<FlightTestResponse>.Ok(await tests.ClaimAsync(gatewayId, Gateway, id, ct)));
    [HttpPost("{id:int}/result")]
    public async Task<ActionResult<ApiResponse<FlightTestResponse>>> Result(int gatewayId, int id, FlightTestResultRequest request, CancellationToken ct) =>
        Ok(ApiResponse<FlightTestResponse>.Ok(await tests.ResultAsync(gatewayId, Gateway, id, request, ct)));
}
