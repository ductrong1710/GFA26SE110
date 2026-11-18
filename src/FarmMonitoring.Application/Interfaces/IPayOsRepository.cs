using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IPayOsRepository
{
    Task<(PayOsTransaction Transaction, bool Created)> ReserveAsync(int consultationId, int revision, int actor, DateTimeOffset now, CancellationToken ct);
    Task SaveLinkAsync(long orderCode, string paymentLinkId, string checkoutUrl, string qrCode, DateTimeOffset? expiresAt, CancellationToken ct);
    Task MarkUncertainAsync(long orderCode, CancellationToken ct);
    Task<PayOsTransaction?> FindAsync(long orderCode, int actor, bool administrator, CancellationToken ct);
    Task<string?> GetPublicStatusAsync(string token, CancellationToken ct);
    Task<bool> ApplyVerifiedAsync(PayOsVerifiedWebhook webhook, DateTimeOffset verifiedAt, CancellationToken ct);
    Task ApplyReconciliationAsync(long orderCode, PayOsProviderStatus status, DateTimeOffset verifiedAt, CancellationToken ct);
}
