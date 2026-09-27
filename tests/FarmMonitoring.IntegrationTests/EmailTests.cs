using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class EmailTests(ApiFactory factory)
{
    private sealed class FakeSender : IEmailSender
    {
        public ConcurrentBag<int> Sent { get; } = [];
        public bool Fail { get; set; }
        public Func<Task>? AfterSend { get; set; }
        public async Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            if (Fail) throw new InvalidOperationException("Secret transport detail must not be persisted");
            Sent.Add(message.NotificationId);
            if (AfterSend is not null) await AfterSend();
        }
    }

    [Fact]
    public async Task Delivery_rechecks_recipient_between_messages()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Email = Guid.NewGuid().ToString("N") + "@example.com", FullName = "Email recipient", PasswordHash = "unused", CreatedAt = DateTimeOffset.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var first = new Notification { UserId = user.Id, Channel = NotificationChannel.EMAIL, Message = "First", Status = NotificationStatus.PENDING, CreatedAt = DateTimeOffset.UtcNow };
        var second = new Notification { UserId = user.Id, Channel = NotificationChannel.EMAIL, Message = "Second", Status = NotificationStatus.PENDING, CreatedAt = DateTimeOffset.UtcNow };
        db.Notifications.Add(first); await db.SaveChangesAsync();
        db.Notifications.Add(second); await db.SaveChangesAsync();
        var sender = new FakeSender { AfterSend = async () =>
        {
            await using var editScope = factory.Services.CreateAsyncScope();
            await editScope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        } };
        await using var workerScope = factory.Services.CreateAsyncScope();
        var service = new EmailDeliveryService(workerScope.ServiceProvider.GetRequiredService<IEmailDeliveryRepository>(), sender,
            new EmailDeliverySettings { Enabled = true }, TimeProvider.System);
        await service.RunOnceAsync(default);
        Assert.Contains(first.Id, sender.Sent);
        Assert.DoesNotContain(second.Id, sender.Sent);
        Assert.Equal(NotificationStatus.FAILED, (await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == second.Id)).Status);
    }

    [Fact]
    public async Task Background_worker_delivers_pending_email()
    {
        var sender = new FakeSender();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(sender))
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Enabled"] = "true", ["Email:WorkerEnabled"] = "true", ["Email:IntervalSeconds"] = "1",
                ["Email:Host"] = "localhost", ["Email:FromAddress"] = "alerts@example.com"
            })));
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
        var notification = new Notification { UserId = userId, Channel = NotificationChannel.EMAIL, Message = "Background delivery",
            Status = NotificationStatus.PENDING, CreatedAt = DateTimeOffset.UtcNow };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        var sent = false;
        for (var i = 0; i < 30 && !sent; i++)
        {
            await Task.Delay(200);
            sent = await db.Notifications.AsNoTracking().AnyAsync(x => x.Id == notification.Id && x.Status == NotificationStatus.SENT);
        }
        Assert.True(sent);
        Assert.Contains(notification.Id, sender.Sent);
    }

    [Fact]
    public async Task Configured_email_is_queued_with_committed_alert()
    {
        var sender = new FakeSender();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(sender)).ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Enabled"] = "true", ["Email:WorkerEnabled"] = "false",
                ["Email:Host"] = "localhost", ["Email:FromAddress"] = "alerts@example.com"
            })));
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Email gateway", GatewayType = "ESP32", CreatedAt = DateTimeOffset.UtcNow };
        db.Gateways.Add(gateway);
        await db.SaveChangesAsync();
        var repo = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        await repo.InTransactionAsync(async () =>
        {
            await scope.ServiceProvider.GetRequiredService<AlertService>().RaiseAsync(new(AlertType.GATEWAY_ERROR,
                AlertSeverity.CRITICAL, null, null, gateway.Id, null, null, "Gateway failed", null), default);
            await repo.SaveAsync(default);
            return true;
        }, default);
        var alert = await db.Alerts.SingleAsync(x => x.GatewayId == gateway.Id);
        var web = await db.Notifications.CountAsync(x => x.AlertId == alert.Id && x.Channel == NotificationChannel.WEB);
        var email = await db.Notifications.Where(x => x.AlertId == alert.Id && x.Channel == NotificationChannel.EMAIL).ToArrayAsync();
        Assert.True(web > 0);
        Assert.Equal(web, email.Length);
        Assert.All(email, x => Assert.Equal(NotificationStatus.PENDING, x.Status));

        async Task Drain()
        {
            await using var workerScope = host.Services.CreateAsyncScope();
            await workerScope.ServiceProvider.GetRequiredService<EmailDeliveryService>().RunOnceAsync(default);
        }
        await Task.WhenAll(Drain(), Drain());
        Assert.Equal(email.Length, sender.Sent.Count);
        Assert.Equal(email.Length, sender.Sent.Distinct().Count());
        var delivered = await db.Notifications.AsNoTracking().Where(x => x.AlertId == alert.Id && x.Channel == NotificationChannel.EMAIL).ToArrayAsync();
        Assert.All(delivered, x => { Assert.Equal(NotificationStatus.SENT, x.Status); Assert.NotNull(x.SentAt); Assert.Null(x.ErrorMessage); });

        var failure = new Notification { AlertId = alert.Id, UserId = email[0].UserId, Channel = NotificationChannel.EMAIL,
            Subject = "Failure test", Message = "Failure test", Status = NotificationStatus.PENDING, CreatedAt = DateTimeOffset.UtcNow };
        // A separate alert avoids violating the per-alert recipient uniqueness constraint.
        failure.AlertId = null;
        db.Notifications.Add(failure);
        await db.SaveChangesAsync();
        sender.Fail = true;
        await Drain();
        var failed = await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == failure.Id);
        Assert.Equal(NotificationStatus.FAILED, failed.Status);
        Assert.Null(failed.SentAt);
        Assert.DoesNotContain("Secret", failed.ErrorMessage!);
        Assert.True(await db.Alerts.AnyAsync(x => x.Id == alert.Id));
        await Drain();
        Assert.Equal(email.Length, sender.Sent.Count);
    }
}
