using System.Globalization;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed record PayOsCheckout(long OrderCode, string PaymentLinkId, string CheckoutUrl, string QrCode, string Status, DateTimeOffset ExpiresAt);
public sealed record PayOsTransactionView(long OrderCode, string? PaymentLinkId, long AmountVnd, string Status, string CheckoutUrl, string? QrCode, DateTimeOffset ExpiresAt, DateTimeOffset? PaidAt, long? ProviderAmountVnd, string? ProviderCode, string? ProviderReference);
public sealed record PayOsAck(int Error, string Message, object? Data);

public sealed class PayOsService(IPayOsRepository repository, IPayOsClient client, ICurrentUser user, TimeProvider clock)
{
    public static bool IsUnresolved(FarmMonitoring.Domain.Entities.PayOsTransaction x) => x.Status is "CREATING" or "PENDING" or "RECONCILIATION_REQUIRED";
    public static string DisplayStatus(FarmMonitoring.Domain.Entities.PayOsTransaction x, DateTimeOffset now) =>
        x.Status == "PENDING" && x.ExpiresAt <= now ? "RECONCILIATION_REQUIRED" : x.Status;
    public async Task<PayOsCheckout> CheckoutAsync(int id, int revision, CancellationToken ct)
    {
        if (revision <= 0) throw new BusinessRuleException("expectedRevision is required.");
        if (!client.IsConfigured) throw new BusinessRuleException("PayOS is not configured.");
        var now = clock.GetUtcNow();
        var (row, created) = await repository.ReserveAsync(id, revision, user.UserId ?? throw new AuthException("Authentication required."), now, ct);
        if (!created) return new(row.OrderCode, row.PaymentLinkId ?? "", row.CheckoutUrl, row.QrCode ?? "", row.Status, row.ExpiresAt);
        try
        {
            var link = await client.CreatePaymentLinkAsync(row.OrderCode, row.AmountVnd, $"TVVU-{row.OrderCode}", row.PublicStatusToken!, row.ExpiresAt, ct);
            await repository.SaveLinkAsync(row.OrderCode, link.PaymentLinkId, link.CheckoutUrl, link.QrCode, link.ExpiresAt, ct);
            return new(row.OrderCode, link.PaymentLinkId, link.CheckoutUrl, link.QrCode, "PENDING", link.ExpiresAt ?? row.ExpiresAt);
        }
        catch
        {
            // Provider timeout may have created a link. Keep the order locked for explicit reconciliation.
            await repository.MarkUncertainAsync(row.OrderCode, CancellationToken.None);
            throw;
        }
    }

    public async Task<PayOsAck> WebhookAsync(string rawJson, CancellationToken ct)
    {
        var webhook = await client.VerifyWebhookAsync(rawJson, ct);
        if (webhook.OrderCode <= 0) return new(0, "Webhook registration payload acknowledged.", null);
        var handled = await repository.ApplyVerifiedAsync(webhook, clock.GetUtcNow(), ct);
        // Unknown orders include PayOS's URL registration test callback and are safely acknowledged without creating receipts.
        return new(0, handled ? "Webhook processed." : "Webhook acknowledged.", null);
    }

    public async Task<PayOsTransactionView> StatusAsync(long orderCode, int actor, bool administrator, CancellationToken ct)
    {
        var row = await repository.FindAsync(orderCode, actor, administrator, ct) ?? throw new NotFoundException("Payment not found.");
        return View(row);
    }

    public async Task<PayOsTransactionView> ReconcileAsync(long orderCode, int actor, CancellationToken ct)
    {
        var row = await repository.FindAsync(orderCode, actor, true, ct) ?? throw new NotFoundException("Payment not found.");
        var provider = await client.GetPaymentStatusAsync(orderCode, ct);
        await repository.ApplyReconciliationAsync(orderCode, provider, clock.GetUtcNow(), ct);
        var updated = await repository.FindAsync(orderCode, actor, true, ct) ?? row;
        return View(updated);
    }

    public async Task<string> PublicStatusAsync(string token, CancellationToken ct) =>
        await repository.GetPublicStatusAsync(token, ct) ?? throw new NotFoundException("Payment status not found.");

    private static PayOsTransactionView View(FarmMonitoring.Domain.Entities.PayOsTransaction x) =>
        new(x.OrderCode, x.PaymentLinkId, x.AmountVnd, x.Status, x.CheckoutUrl, x.QrCode, x.ExpiresAt, x.PaidAt, x.ProviderAmountVnd, x.ProviderCode, x.ProviderReference);
}
