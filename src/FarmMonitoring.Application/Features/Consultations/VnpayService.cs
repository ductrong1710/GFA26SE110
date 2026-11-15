using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed record VnpayCheckout(string CheckoutUrl,string TxnRef,string Status,DateTimeOffset ExpiresAt,bool Sandbox = true);
public sealed record VnpayStatus(string Status,bool Sandbox = true);
public sealed record VnpayIpn([property: JsonPropertyName("RspCode")] string RspCode,
    [property: JsonPropertyName("Message")] string Message);
public sealed record VnpayAttemptView(string TxnRef,string Status,DateTimeOffset ExpiresAt,string? CheckoutUrl);

public sealed class VnpayService(ISiteVisitRepository visits, IVnpayRepository repository, ICurrentUser user,
    TimeProvider clock, VnpayProtocol protocol)
{
    public Task<VnpayCheckout> CheckoutAsync(int id, int? expectedRevision, string ip, CancellationToken ct) =>
        visits.WithVisitAsync<VnpayCheckout>(id, user.UserId ?? throw new AuthException("Authentication required."), VisitAccess.Administrator, true,
            (lead,revisions,admin) =>
            {
                if (!protocol.IsConfigured) throw new BusinessRuleException("VNPAY Sandbox is disabled or not configured.");
                if (expectedRevision is null or <= 0) throw new ValidationException("expectedRevision is required.");
                var current = revisions.LastOrDefault();
                if (current is null || current.Revision != expectedRevision) throw new ConflictException("Reload the current revision.");
                if (lead.Channel != "ON_SITE" || current.Channel != "ON_SITE" || current.ReviewStatus != "APPROVED"
                    || current.AmountVnd <= 0 || current.Payment != null || current.ScheduledAt != null)
                    throw new ConflictException("An approved unpaid positive on-site fee is required.");
                var attempts = revisions.SelectMany(x => x.Transactions).ToArray();
                var blocking = attempts.Where(IsUnresolved).ToArray();
                var now = clock.GetUtcNow();
                if (current.ProposedAt <= now) throw new ConflictException("Ngày giờ tư vấn đã qua.");
                if (blocking.Length == 1 && blocking[0] is { Status: "PENDING" } pending && pending.ProposalId == current.Id
                    && pending.ExpiresAt > now && pending.TmnCode == protocol.TmnCode)
                    return new(pending.CheckoutUrl,pending.TxnRef,pending.Status,pending.ExpiresAt);
                if (blocking.Length != 0) throw new ConflictException("Payment outcome is unknown. Reconcile with VNPAY QueryDr before another payment.");
                var row = CreateAttempt(current, protocol, user.UserId!.Value, now, ip);
                return new(row.CheckoutUrl,row.TxnRef,row.Status,row.ExpiresAt);
            }, ct);

    internal static VnpayTransaction CreateAttempt(SiteVisitProposal proposal, VnpayProtocol protocol, int actor, DateTimeOffset now, string ip)
    {
        var row = new VnpayTransaction {
            TxnRef = VnpayProtocol.NewTransactionReference(), ProposalId = proposal.Id, AmountVnd = proposal.AmountVnd,
            TmnCode = protocol.TmnCode!, CreatedAt = now, ExpiresAt = now.AddMinutes(protocol.ExpiryMinutes),
            CreatedBy = actor, PublicToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
        };
        row.CheckoutUrl = protocol.BuildPayUrl(row.TxnRef,row.AmountVnd,row.CreatedAt,row.ExpiresAt,ip,row.PublicToken);
        proposal.Transactions.Add(row);

        return row;
    }

    public async Task<VnpayStatus> StatusAsync(string token,CancellationToken ct)
    {
        if (token.Length != 64 || !token.All(char.IsAsciiHexDigit)) throw new NotFoundException("Payment not found.");
        return new(await repository.PublicStatusAsync(token,clock.GetUtcNow(),ct) ?? throw new NotFoundException("Payment not found."));
    }

    public Task<VnpayIpn> IpnAsync(IReadOnlyDictionary<string,string> fields,CancellationToken ct)
    {
        if (!protocol.ValidateCallback(fields)) return Task.FromResult(new VnpayIpn("97","Invalid signature"));
        if (!fields.TryGetValue("vnp_TxnRef",out var txnRef) || txnRef.Length is 0 or >100)
            return Task.FromResult(new VnpayIpn("01","Order not found"));
        return repository.ApplyAsync(txnRef, (lead,revisions,row) =>
        {
            if (!fields.TryGetValue("vnp_TmnCode",out var merchant) || row.TmnCode != merchant) return new("97","Invalid merchant");
            if (!fields.TryGetValue("vnp_Amount",out var rawAmount)
                || !long.TryParse(rawAmount,NumberStyles.None,CultureInfo.InvariantCulture,out var amount)
                || amount != VnpayProtocol.ToProviderAmount(row.AmountVnd))
                return new("04","Invalid amount");
            if (!fields.TryGetValue("vnp_ResponseCode",out var response) || !fields.TryGetValue("vnp_TransactionStatus",out var status)
                || response.Length != 2 || status.Length != 2) return new("99","Invalid result");
            if (row.Status is "SUCCEEDED" or "RECONCILIATION_REQUIRED") return new("02","Order already confirmed");
            if (row.Status == "FAILED")
            {
                if (response == row.ResponseCode && status == row.TransactionStatus) return new("02","Order already confirmed");
                row.Status = "RECONCILIATION_REQUIRED";
                row.VerifiedAt = clock.GetUtcNow();
                return new("00","Recorded for reconciliation");
            }
            var current = revisions[^1];
            var oldRevision = current.Id != row.ProposalId || current.ReviewStatus != "APPROVED"
                || current.Channel != "ON_SITE" || lead.Channel != "ON_SITE" || lead.Status != ConsultationStatus.IN_PROGRESS;
            var success = response == "00" && status == "00";
            var no = fields.GetValueOrDefault("vnp_TransactionNo");
            DateTimeOffset? paidAt = null;
            if (success)
            {
                if (no is not { Length: > 0 and <= 15 } || !no.All(char.IsAsciiDigit) || no.All(c => c == '0')
                    || !DateTimeOffset.TryParseExact(fields.GetValueOrDefault("vnp_PayDate") + "+07:00","yyyyMMddHHmmsszzz",
                        CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))
                    return new("99","Invalid successful result");
                paidAt = date.ToUniversalTime();
            }
            row.ResponseCode = response;
            row.TransactionStatus = status;
            row.ProviderTransactionNo = no;
            row.PayDate = paidAt;
            row.VerifiedAt = clock.GetUtcNow();
            if (oldRevision || current.Payment != null || current.ScheduledAt != null
                || revisions.SelectMany(p => p.Transactions).Any(t => t.TxnRef != row.TxnRef && IsUnresolved(t))
                || (success && (paidAt < row.CreatedAt.AddSeconds(-1) || paidAt > row.ExpiresAt)))
                row.Status = "RECONCILIATION_REQUIRED";
            else if (success)
            {
                row.Status = "SUCCEEDED";
                current.Payment = new SiteVisitPayment { Source = "VNPAY_SANDBOX", VnpayTxnRef = row.TxnRef,
                    PaidAmountVnd = row.AmountVnd, PaidAt = paidAt!.Value, Reference = no!,
                    VerifiedAt = row.VerifiedAt.Value, VerifiedBy = null };
            }
            else if (status == "02" && response is "09" or "10" or "11" or "12" or "13" or "24" or "51" or "65" or "75" or "79")
                row.Status = "FAILED";
            else row.Status = "RECONCILIATION_REQUIRED";
            return new("00","Confirm Success");
        }, () => new VnpayIpn("01","Order not found"), ct);
    }

    public static bool IsUnresolved(VnpayTransaction row) => row.Status is "PENDING" or "RECONCILIATION_REQUIRED";
    public static void RequireNoUnresolved(IEnumerable<SiteVisitProposal> revisions)
    {
        if (revisions.SelectMany(p => p.Transactions).Any(IsUnresolved))
            throw new ConflictException("VNPAY outcome is unresolved. Use QueryDr before recording another payment or scheduling.");
    }
    public static VnpayAttemptView View(VnpayTransaction row,DateTimeOffset now) =>
        new(row.TxnRef,row.Status == "PENDING" && row.ExpiresAt <= now ? "UNKNOWN" : row.Status,row.ExpiresAt,
            row.Status == "PENDING" && row.ExpiresAt > now ? row.CheckoutUrl : null);
}
