using System.Globalization;
using System.Security.Cryptography;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed partial class ConsultationRepository : IPayOsRepository
{
    public Task<(PayOsTransaction Transaction, bool Created)> ReserveAsync(int consultationId, int revision, int actor, DateTimeOffset now, CancellationToken ct) =>
        WithVisitAsync(consultationId, actor, VisitAccess.Administrator, true, (lead, revisions, _) =>
        {
            var proposal = revisions.LastOrDefault();
            if (proposal is null || proposal.Revision != revision || lead.Channel != "ON_SITE" || proposal.Channel != "ON_SITE"
                || proposal.ReviewStatus != "APPROVED" || proposal.AmountVnd <= 0 || proposal.Payment != null || proposal.ScheduledAt != null
                || proposal.ProposedAt <= now)
                throw new ConflictException("An approved, unpaid, future on-site proposal is required.");

            var all = revisions.SelectMany(x => x.PayOsTransactions).ToArray();
            var active = all.Where(x => x.Status is "CREATING" or "PENDING" or "RECONCILIATION_REQUIRED").ToArray();
            if (active.Length != 0)
            {
                if (active.Length == 1 && active[0].ProposalId == proposal.Id && active[0].Status == "PENDING"
                    && active[0].ExpiresAt > now && !string.IsNullOrWhiteSpace(active[0].CheckoutUrl))
                    return (active[0], false);
                throw new ConflictException("An existing PayOS order is unresolved. Reconcile it before creating another link.");
            }

            var orderCode = checked(now.ToUnixTimeMilliseconds() * 1_000 + RandomNumberGenerator.GetInt32(0, 1_000));
            var row = new PayOsTransaction { OrderCode = orderCode, ProposalId = proposal.Id, AmountVnd = proposal.AmountVnd,
                PublicStatusToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
                CreatedAt = now, ExpiresAt = now.AddMinutes(30), CreatedBy = actor, Status = "CREATING" };
            proposal.PayOsTransactions.Add(row);
            return (row, true);
        }, ct);

    public async Task SaveLinkAsync(long orderCode, string paymentLinkId, string checkoutUrl, string qrCode, DateTimeOffset? expiresAt, CancellationToken ct)
    {
        var row = await db.Set<PayOsTransaction>().SingleAsync(x => x.OrderCode == orderCode, ct);
        if (row.Status is not ("CREATING" or "RECONCILIATION_REQUIRED")) return;
        row.PaymentLinkId = paymentLinkId;
        row.CheckoutUrl = checkoutUrl;
        row.QrCode = qrCode;
        if (expiresAt is not null) row.ExpiresAt = expiresAt.Value;
        if (row.Status == "CREATING") row.Status = "PENDING";
        var ownership = await db.Set<SiteVisitProposal>().Where(x => x.Id == row.ProposalId)
            .Select(x => new { x.Consultation.EngineerId, x.ConsultationId }).SingleAsync(ct);
        if (ownership.EngineerId is int assigned)
            await WorkflowNotifications.AddForUserAsync(db, assigned, "Đã tạo link thanh toán PayOS",
                $"Mở yêu cầu tư vấn #{ownership.ConsultationId} để sao chép link thanh toán cho khách hàng.", row.CreatedAt, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkUncertainAsync(long orderCode, CancellationToken ct)
    {
        var row = await db.Set<PayOsTransaction>().SingleOrDefaultAsync(x => x.OrderCode == orderCode, ct);
        if (row is not null && row.Status is "CREATING" or "PENDING")
        {
            row.Status = "RECONCILIATION_REQUIRED";
            row.VerifiedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<PayOsTransaction?> FindAsync(long orderCode, int actor, bool administrator, CancellationToken ct)
    {
        var row = await db.Set<PayOsTransaction>().Include(x => x.Proposal)
            .ThenInclude(x => x.Consultation).SingleOrDefaultAsync(x => x.OrderCode == orderCode, ct);
        if (row is null) return null;
        var consultation = row.Proposal.Consultation;
        if (!administrator && consultation.EngineerId != actor) throw AccessDeniedException.OutsideScope();
        return row;
    }

    public async Task<string?> GetPublicStatusAsync(string token, CancellationToken ct)
    {
        var row = await db.Set<PayOsTransaction>().AsNoTracking()
            .Where(x => x.PublicStatusToken == token)
            .Select(x => new { x.Status, x.ExpiresAt })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;
        return row.Status == "PENDING" && row.ExpiresAt <= DateTimeOffset.UtcNow ? "RECONCILIATION_REQUIRED" : row.Status;
    }

    public async Task<bool> ApplyVerifiedAsync(PayOsVerifiedWebhook webhook, DateTimeOffset verifiedAt, CancellationToken ct)
    {
        var consultationId = await db.Set<PayOsTransaction>().Where(x => x.OrderCode == webhook.OrderCode)
            .Select(x => (int?)x.Proposal.ConsultationId).SingleOrDefaultAsync(ct);
        if (consultationId is null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var lead = await LockAsync(consultationId.Value, ct);
        var revisions = await db.Set<SiteVisitProposal>().Include(x => x.Payment).Include(x => x.PayOsTransactions)
            .Where(x => x.ConsultationId == consultationId).OrderBy(x => x.Revision).ToListAsync(ct);
        var row = revisions.SelectMany(x => x.PayOsTransactions).SingleOrDefault(x => x.OrderCode == webhook.OrderCode);
        if (row is null) return false;
        if (row.Status == "SUCCEEDED") { await tx.CommitAsync(ct); return true; }
        row.ProviderReference = webhook.Reference;
        row.ProviderPaymentLinkId = webhook.PaymentLinkId;
        row.ProviderCode = webhook.Code;
        row.ProviderAmountVnd = webhook.Amount;
        row.ProviderTransactionDateTime = webhook.TransactionDateTime;
        if (row.Status == "EXPIRED" && webhook.Success)
        {
            row.Status = "RECONCILIATION_REQUIRED"; row.VerifiedAt = verifiedAt;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }

        var current = revisions.LastOrDefault();
        var amountMatches = webhook.Amount == row.AmountVnd;
        var linkMatches = row.PaymentLinkId == webhook.PaymentLinkId;
        if (!amountMatches || !linkMatches || !webhook.Success || webhook.Code != "00")
        {
            // Ignore provider non-payment notifications; a mismatch on an apparent success needs human reconciliation.
            if (webhook.Success && webhook.Code == "00") { row.Status = "RECONCILIATION_REQUIRED"; row.VerifiedAt = verifiedAt; }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }

        if (current?.Id != row.ProposalId || current.ReviewStatus != "APPROVED" || current.Channel != "ON_SITE"
            || lead.Channel != "ON_SITE" || lead.Status != ConsultationStatus.IN_PROGRESS || current.Payment is not null
            || current.ScheduledAt is not null || revisions.SelectMany(x => x.PayOsTransactions).Any(x => x.OrderCode != row.OrderCode && x.Status is "PENDING" or "CREATING" or "RECONCILIATION_REQUIRED")
            || !TryParseProviderDateTime(webhook.TransactionDateTime, out var paidAt)
            || paidAt < row.CreatedAt.AddMinutes(-2) || paidAt > row.ExpiresAt.AddDays(1))
        {
            row.Status = "RECONCILIATION_REQUIRED"; row.VerifiedAt = verifiedAt;
        }
        else
        {
            row.Status = "SUCCEEDED"; row.VerifiedAt = verifiedAt; row.PaidAt = paidAt.ToUniversalTime();
            row.ProviderReference = webhook.Reference; row.ProviderCode = webhook.Code;
            current.Payment = new SiteVisitPayment { Source = "PAYOS", PaidAmountVnd = row.AmountVnd,
                PaidAt = row.PaidAt.Value, Reference = webhook.Reference, VerifiedAt = verifiedAt, VerifiedBy = null,
                PayOsTransactionId = row.Id };
            await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Đã nhận thanh toán tư vấn tại vườn",
                $"Yêu cầu tư vấn #{lead.Id}, số tiền {row.AmountVnd:N0} VND đã được PayOS xác nhận lúc {row.PaidAt.Value:dd/MM/yyyy HH:mm} UTC. Trạng thái: thành công.", verifiedAt, ct);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task ApplyReconciliationAsync(long orderCode, PayOsProviderStatus status, DateTimeOffset verifiedAt, CancellationToken ct)
    {
        var callback = new PayOsVerifiedWebhook(status.Status == "Paid", status.Status == "Paid" ? "00" : status.Status,
            orderCode, status.AmountPaid, status.PaymentLinkId, status.Reference, status.TransactionDateTime);
        await ApplyVerifiedAsync(callback, verifiedAt, ct);
        if (status.Status is "Cancelled" or "Failed")
        {
            var row = await db.Set<PayOsTransaction>().SingleOrDefaultAsync(x => x.OrderCode == orderCode, ct);
            if (row is not null && row.Status != "SUCCEEDED") { row.Status = "FAILED"; row.VerifiedAt = verifiedAt; row.ProviderCode = status.Status; await db.SaveChangesAsync(ct); }
        }
        else if (status.Status is not ("Paid" or "Pending" or "Expired"))
        {
            var row = await db.Set<PayOsTransaction>().SingleOrDefaultAsync(x => x.OrderCode == orderCode, ct);
            if (row is not null && row.Status != "SUCCEEDED") { row.Status = "RECONCILIATION_REQUIRED"; row.VerifiedAt = verifiedAt; await db.SaveChangesAsync(ct); }
        }
        else if (status.Status == "Expired")
        {
            var row = await db.Set<PayOsTransaction>().SingleOrDefaultAsync(x => x.OrderCode == orderCode, ct);
            if (row is not null && row.Status != "SUCCEEDED") { row.Status = "EXPIRED"; row.VerifiedAt = verifiedAt; row.ProviderCode = "Expired"; await db.SaveChangesAsync(ct); }
        }
    }

    private static bool TryParseProviderDateTime(string value, out DateTimeOffset result)
    {
        // payOS webhook examples use a timezone-free local timestamp; payment channels are Vietnam-based (UTC+7).
        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            result = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).ToUniversalTime();
            return true;
        }
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
    }
}
