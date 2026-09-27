using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Features.Alerts;

public sealed class EmailDeliverySettings
{
    public bool Enabled { get; set; }
    public bool WorkerEnabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 100;
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class EmailDeliveryService(IEmailDeliveryRepository repository, IEmailSender sender, EmailDeliverySettings settings, TimeProvider clock)
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (!settings.Enabled) return;
        for (var i = 0; i < settings.BatchSize; i++)
        {
            var processed = await repository.ProcessNextAsync(async notification =>
            {
                if (!notification.User.IsActive)
                {
                    notification.Status = NotificationStatus.FAILED;
                    notification.ErrorMessage = "Recipient account is inactive.";
                    return;
                }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                try
                {
                    await sender.SendAsync(new(notification.Id, notification.User.Email, notification.Subject ?? "Farm monitoring alert", notification.Message), timeout.Token);
                    notification.Status = NotificationStatus.SENT;
                    notification.SentAt = clock.GetUtcNow();
                    notification.ErrorMessage = null;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    notification.Status = NotificationStatus.FAILED;
                    notification.ErrorMessage = "Email delivery failed. Check mail service configuration and availability.";
                }
            }, ct);
            if (!processed) break;
        }
    }
}
