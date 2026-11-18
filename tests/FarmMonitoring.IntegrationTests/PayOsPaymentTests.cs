using System.Globalization;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public sealed class PayOsPaymentTests(ApiFactory factory)
{
    [Fact]
    public async Task Concurrent_verified_webhooks_create_one_receipt_and_one_admin_notification()
    {
        int proposalId;
        long orderCode;
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var engineerId = await db.Users.Where(x => x.Email == "engineer@example.com").Select(x => x.Id).SingleAsync();
            var adminId = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync();
            var lead = new Consultation
            {
                CustomerName = "PayOS integration fixture", Phone = "0901234567", FarmLocation = "Test farm",
                ServiceNeed = "Monitoring", Content = "On-site consultation", Channel = "ON_SITE",
                Status = ConsultationStatus.IN_PROGRESS, EngineerId = engineerId, CreatedAt = now
            };
            db.Consultations.Add(lead);
            await db.SaveChangesAsync();

            var proposal = new SiteVisitProposal
            {
                ConsultationId = lead.Id, Revision = 1, Channel = "ON_SITE", AmountVnd = 245000,
                PricingMethod = "CALCULATED", CreatedBy = engineerId, CreatedAt = now.AddMinutes(-2),
                ReviewStatus = "APPROVED", ReviewedBy = adminId, ReviewedAt = now.AddMinutes(-1),
                ProposedAt = now.AddDays(3)
            };
            db.Set<SiteVisitProposal>().Add(proposal);
            await db.SaveChangesAsync();
            proposalId = proposal.Id;

            orderCode = now.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(1000);
            db.Set<PayOsTransaction>().Add(new PayOsTransaction
            {
                OrderCode = orderCode, ProposalId = proposal.Id, AmountVnd = proposal.AmountVnd,
                PaymentLinkId = "payos-link-" + Guid.NewGuid().ToString("N"), CheckoutUrl = "https://payos.invalid/checkout",
                CreatedBy = adminId, CreatedAt = now.AddMinutes(-1), ExpiresAt = now.AddMinutes(20), Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        var providerReference = "payos-ref-" + Guid.NewGuid().ToString("N");
        string linkId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            linkId = await db.Set<PayOsTransaction>().Where(x => x.OrderCode == orderCode).Select(x => x.PaymentLinkId!).SingleAsync();
        }
        var webhook = new PayOsVerifiedWebhook(true, "00", orderCode, 245000, linkId, providerReference,
            now.AddHours(7).AddSeconds(-5).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        var calls = await Task.WhenAll(Apply(factory, webhook, now), Apply(factory, webhook, now));

        Assert.All(calls, result => Assert.True(result));
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var receipt = await verifyDb.Set<SiteVisitPayment>().SingleAsync(x => x.ProposalId == proposalId);
        Assert.Equal("PAYOS", receipt.Source);
        Assert.Equal(245000, receipt.PaidAmountVnd);
        Assert.NotNull(receipt.PayOsTransactionId);
        var activeAdmins = await verifyDb.Users.CountAsync(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == RoleNames.FarmAdministrator));
        Assert.Equal(activeAdmins, await verifyDb.Notifications.CountAsync(x => x.Subject == "Đã nhận thanh toán tư vấn tại vườn" && x.Channel == NotificationChannel.WEB));
        Assert.Equal("SUCCEEDED", await verifyDb.Set<PayOsTransaction>().Where(x => x.OrderCode == orderCode).Select(x => x.Status).SingleAsync());
    }

    private static async Task<bool> Apply(ApiFactory factory, PayOsVerifiedWebhook webhook, DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPayOsRepository>().ApplyVerifiedAsync(webhook, now, default);
    }
}
