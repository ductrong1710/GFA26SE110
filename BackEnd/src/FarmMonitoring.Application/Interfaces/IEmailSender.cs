namespace FarmMonitoring.Application.Interfaces;

public sealed record EmailMessage(int NotificationId, string Recipient, string Subject, string Body);
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
