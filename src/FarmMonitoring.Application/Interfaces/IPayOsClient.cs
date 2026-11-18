namespace FarmMonitoring.Application.Interfaces;

public sealed record PayOsLinkResult(string PaymentLinkId, string CheckoutUrl, string QrCode, DateTimeOffset? ExpiresAt);
public sealed record PayOsVerifiedWebhook(bool Success, string Code, long OrderCode, long Amount, string PaymentLinkId,
    string Reference, string TransactionDateTime);
public sealed record PayOsProviderStatus(string Status, long AmountPaid, string PaymentLinkId, string Reference, string TransactionDateTime);

public interface IPayOsClient
{
    bool IsConfigured { get; }
    Task<PayOsLinkResult> CreatePaymentLinkAsync(long orderCode, long amountVnd, string description, string publicStatusToken, DateTimeOffset expiresAt, CancellationToken ct);
    Task<PayOsVerifiedWebhook> VerifyWebhookAsync(string rawJson, CancellationToken ct);
    Task<PayOsProviderStatus> GetPaymentStatusAsync(long orderCode, CancellationToken ct);
}
