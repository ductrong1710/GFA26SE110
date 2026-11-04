using System.Net;
using System.Net.Sockets;
using System.Text;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.IntegrationTests;

public class SmtpTransportTests
{
    [Fact]
    public async Task Configured_sender_delivers_the_operational_message_to_an_SMTP_receiver()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions {
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            FromAddress = "farm@example.test", EnableSsl = false
        }));
        var mail = new EmailMessage(1, "owner@example.test", "Maintenance due", "Inspect the orchard gateway.");
        var receive = ReceiveAsync(listener, deadline.Token);
        await sender.SendAsync(mail, deadline.Token);
        var received = await receive;
        Assert.Contains("MAIL FROM:<farm@example.test>", received, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RCPT TO:<owner@example.test>", received, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Subject: " + mail.Subject, received);
        Assert.Contains(mail.Body, received);
    }

    private static async Task<string> ReceiveAsync(TcpListener listener, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        var transcript = new StringBuilder();
        await writer.WriteLineAsync("220 localhost ready");
        while (await reader.ReadLineAsync(ct) is { } command)
        {
            transcript.AppendLine(command);
            if (command == "DATA")
            {
                await writer.WriteLineAsync("354 End data with a dot");
                while (await reader.ReadLineAsync(ct) is { } line && line != ".")
                    transcript.AppendLine(line);
            }
            if (command == "QUIT")
            {
                await writer.WriteLineAsync("221 Bye");
                break;
            }
            await writer.WriteLineAsync("250 OK");
        }
        return transcript.ToString();
    }
}
