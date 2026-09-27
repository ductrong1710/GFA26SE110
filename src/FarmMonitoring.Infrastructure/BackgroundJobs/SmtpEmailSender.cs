using System.Net;
using System.Net.Mail;
using FarmMonitoring.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.Infrastructure.BackgroundJobs;

public sealed class SmtpOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string FromAddress { get; set; } = "";
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool IsValid() => !Enabled || (!string.IsNullOrWhiteSpace(Host) && Port is >= 1 and <= 65535
        && MailAddress.TryCreate(FromAddress, out _) && (string.IsNullOrEmpty(Username) == string.IsNullOrEmpty(Password)));
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var config = options.Value;
        using var client = new SmtpClient(config.Host, config.Port) { EnableSsl = config.EnableSsl, UseDefaultCredentials = false };
        if (!string.IsNullOrEmpty(config.Username)) client.Credentials = new NetworkCredential(config.Username, config.Password);
        using var mail = new MailMessage(config.FromAddress, message.Recipient, message.Subject, message.Body) { IsBodyHtml = false };
        await client.SendMailAsync(mail, ct);
    }
}
