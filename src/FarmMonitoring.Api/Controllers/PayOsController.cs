using System.Text;
using FarmMonitoring.Api.Authorization;
using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Features.Consultations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmMonitoring.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayOsController(PayOsService service, ILogger<PayOsController> logger) : ControllerBase
{
    [HttpPost("/api/consultations/{id:int}/site-visit/payments/payos/checkout")]
    [Authorize(Policy = AccessPolicies.ManageUsers)]
    public async Task<ActionResult<ApiResponse<PayOsCheckout>>> Checkout(int id, SiteVisitScheduleRequest request, CancellationToken ct) =>
        Ok(ApiResponse<PayOsCheckout>.Ok(await service.CheckoutAsync(id, request.ExpectedRevision ?? 0, ct)));

    [HttpGet("/api/payments/payos/{orderCode:long}")]
    [Authorize(Policy = AccessPolicies.ManageUsers)]
    public async Task<ActionResult<ApiResponse<PayOsTransactionView>>> Status(long orderCode, CancellationToken ct) =>
        Ok(ApiResponse<PayOsTransactionView>.Ok(await service.StatusAsync(orderCode, UserId(), true, ct)));

    [HttpGet("/api/payments/payos/public/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> PublicStatus(string token, CancellationToken ct)
    {
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return NotFound();
        return Ok(ApiResponse<object>.Ok(new { status = await service.PublicStatusAsync(token, ct) }));
    }

    [HttpPost("/api/payments/payos/{orderCode:long}/reconcile")]
    [Authorize(Policy = AccessPolicies.ManageUsers)]
    public async Task<ActionResult<ApiResponse<PayOsTransactionView>>> Reconcile(long orderCode, CancellationToken ct) =>
        Ok(ApiResponse<PayOsTransactionView>.Ok(await service.ReconcileAsync(orderCode, UserId(), ct)));

    [HttpPost("/api/payments/payos/webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(65536)]
    [ProducesResponseType(typeof(PayOsAck), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PayOsAck), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var receivedAtUtc = DateTimeOffset.UtcNow;
        logger.LogInformation("PayOS webhook received at {ReceivedAtUtc}", receivedAtUtc);
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, false, 1024, true);
        var raw = await reader.ReadToEndAsync(ct);
        if (raw.Length is 0 or > 65536)
        {
            logger.LogWarning("PayOS webhook rejected before verification. HttpStatusCode={HttpStatusCode}; Reason=InvalidBody", StatusCodes.Status400BadRequest);
            return BadRequest(new PayOsAck(1, "Invalid webhook body.", null));
        }
        try
        {
            var ack = await service.WebhookAsync(raw, ct);
            var outcome = ack.Message switch
            {
                "Webhook registration payload acknowledged." => "RegistrationPayloadAcknowledged",
                "Webhook processed." => "Processed",
                _ => "Acknowledged"
            };
            logger.LogInformation("PayOS webhook acknowledged. HttpStatusCode={HttpStatusCode}; Outcome={Outcome}; AckError={AckError}",
                StatusCodes.Status200OK, outcome, ack.Error);
            return Ok(ack);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("PayOS webhook failed. HttpStatusCode={HttpStatusCode}; ExceptionType={ExceptionType}",
                StatusCodes.Status400BadRequest, ex.GetType().Name);
            return BadRequest(new PayOsAck(1, "Webhook verification or processing failed.", null));
        }
    }

    private int UserId() => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
}
