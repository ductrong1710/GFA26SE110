using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Consultations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VnpayController(VnpayService service, ILogger<VnpayController> logger) : ControllerBase
{
    [HttpPost("/api/consultations/{id:int}/site-visit/payments/vnpay/checkout")]
    [Authorize(Policy = AccessPolicies.ManageUsers)]
    public async Task<ActionResult<ApiResponse<VnpayCheckout>>> Checkout(int id,SiteVisitScheduleRequest request,CancellationToken ct) =>
        Ok(ApiResponse<VnpayCheckout>.Ok(await service.CheckoutAsync(id,request.ExpectedRevision,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",ct)));

    [HttpGet("/api/payments/vnpay/status")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<VnpayStatus>>> Status([FromQuery] string token,CancellationToken ct) =>
        Ok(ApiResponse<VnpayStatus>.Ok(await service.StatusAsync(token,ct)));

    [HttpGet("/api/payments/vnpay/ipn")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VnpayIpn), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ipn(CancellationToken ct)
    {
        if (Request.QueryString.Value?.Length > 16384 || Request.Query.Count > 32
            || Request.Query.Any(x => x.Value.Count != 1))
            return Ok(new VnpayIpn("97","Invalid signature"));
        var fields = Request.Query.ToDictionary(x => x.Key,x => x.Value.ToString(),StringComparer.Ordinal);
        try { return Ok(await service.IpnAsync(fields,ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Do not log the callback URL, token or signature. VNPAY may retry after rollback.
            logger.LogError("VNPAY IPN could not commit ({ExceptionType})",ex.GetType().Name);
            return Ok(new VnpayIpn("99","Unable to commit"));
        }
    }
}
