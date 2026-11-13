using System.Text.Json;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed class SiteVisitService(ISiteVisitRepository repository, ICurrentUser current, TimeProvider clock, SiteVisitPricing pricing)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");

    public Task<SiteVisitState> GetAsync(int id, CancellationToken ct) =>
        repository.WithVisitAsync(id, Actor, VisitAccess.Reader, false, State, ct);

    public Task<SiteVisitFeePreview> PreviewAsync(int id, SiteVisitPreviewRequest request, CancellationToken ct) =>
        repository.WithVisitAsync(id, Actor, VisitAccess.Reader, false, (lead, revisions, admin) =>
            pricing.Preview(request.OfficeCode, request.DistanceKm), ct);

    public Task<SiteVisitState> ChannelAsync(int id, SiteVisitChannelRequest request, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Engineer, (lead, revisions, now) =>
        {
            if (revisions.Count != 0) throw new ConflictException("Submit a new revision to change the channel.");
            if (request.Channel is not ("ONLINE" or "ON_SITE")) throw new ValidationException("Unknown consultation channel.");
            lead.History.Add(new() { Action = ConsultationAction.CHANNEL_CHANGED, Status = lead.Status, UserId = Actor,
                CreatedAt = now, Notes = $"{lead.Channel ?? "UNSPECIFIED"} -> {request.Channel}" });
            lead.Channel = request.Channel;
        }, ct);

    public Task<SiteVisitState> ProposeAsync(int id, SiteVisitProposeRequest request, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Engineer, (lead, revisions, now) =>
        {
            var previous = revisions.LastOrDefault();
            if (previous?.Payment is not null || previous?.ScheduledAt is not null)
                throw new ConflictException("Confirmed receipts and schedules require administrator reconciliation.");
            var channel = request.Channel ?? lead.Channel;
            if (channel is not ("ONLINE" or "ON_SITE") || previous == null && channel != lead.Channel)
                throw new ConflictException("Record the customer's channel before proposing a visit.");
            var reason = Optional(request.Reason, 1000);
            if (channel != lead.Channel && reason is null) throw new ValidationException("Explain the channel adjustment.");
            var proposal = new SiteVisitProposal { ConsultationId = id, Revision = (previous?.Revision ?? 0) + 1, Channel = channel,
                ChangeReason = reason, CreatedBy = Actor, CreatedAt = now };
            if (channel == "ONLINE")
            {
                if (previous is null || lead.Channel == "ONLINE") throw new ConflictException("Online consultations do not require a fee proposal.");
                if (request.OfficeCode != null || request.DistanceKm != null || request.MapsUrl != null || request.ProposedAt != null
                    || request.ManualAmountVnd != null || request.ManualReason != null || request.PricingVersion != null)
                    throw new ValidationException("Online adjustments cannot include travel or fee details.");
                proposal.PricingMethod = "ONLINE";
            }
            else
            {
                var fee = pricing.Preview(request.OfficeCode, request.DistanceKm ?? 0);
                if (fee.Status == "PRICING_NOT_CONFIGURED") throw new BusinessRuleException("PRICING_NOT_CONFIGURED");
                if (fee.PricingVersion != request.PricingVersion || string.IsNullOrWhiteSpace(request.PricingVersion))
                    throw new ConflictException("Pricing changed. Request a fresh fee preview.");
                if (request.ProposedAt is null || request.ProposedAt <= now) throw new ValidationException("A future proposedAt is required.");
                proposal.OfficeCode = fee.OfficeCode;
                proposal.OfficeLabel = fee.OfficeLabel;
                proposal.DistanceKm = fee.DistanceKm;
                proposal.MapsUrl = SiteVisitPricing.ValidateMapsUrl(request.MapsUrl);
                proposal.ProposedAt = request.ProposedAt.Value.ToUniversalTime();
                proposal.PricingVersion = fee.PricingVersion;
                if (fee.Status == "MANUAL_QUOTE_REQUIRED")
                {
                    proposal.ManualReason = Required(request.ManualReason, 1000, "manualReason");
                    if (request.ManualAmountVnd is not > 0) throw new ValidationException("A positive manualAmountVnd is required.");
                    proposal.AmountVnd = request.ManualAmountVnd.Value;
                    proposal.PricingMethod = "MANUAL_QUOTE";
                }
                else
                {
                    if (request.ManualAmountVnd != null || request.ManualReason != null)
                        throw new ValidationException("Manual quotes are only available above 150 km.");
                    proposal.AmountVnd = fee.AmountVnd ?? throw new BusinessRuleException("PRICING_NOT_CONFIGURED");
                    proposal.PricingMethod = "CALCULATED";
                }
            }
            revisions.Add(proposal);
        }, ct);

    public Task<SiteVisitState> ReviewAsync(int id, SiteVisitReviewRequest request, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Administrator, (lead, revisions, now) =>
        {
            var p = Latest(revisions);
            if (p.ReviewStatus != "PENDING") throw new ConflictException("Only a pending revision can be reviewed.");
            if (request.Decision is not ("APPROVE" or "RETURN")) throw new ValidationException("Decision must be APPROVE or RETURN.");
            p.ReviewReason = request.Decision == "RETURN" ? Required(request.Reason, 1000, "reason") : Optional(request.Reason, 1000);
            p.ReviewStatus = request.Decision == "APPROVE" ? "APPROVED" : "RETURNED";
            p.ReviewedAt = now;
            p.ReviewedBy = Actor;
            if (p.ReviewStatus == "APPROVED") lead.Channel = p.Channel;
        }, ct);

    public Task<SiteVisitState> ConfirmAsync(int id, SiteVisitConfirmRequest request, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Administrator, (lead, revisions, now) =>
        {
            var p = Latest(revisions);
            if (p.ReviewStatus != "APPROVED" || p.AmountVnd == 0 || p.Payment != null || p.ScheduledAt != null)
                throw new ConflictException("This revision cannot accept a payment.");
            if (request.PaidAmountVnd != p.AmountVnd) throw new BusinessRuleException("Payment does not match the approved fee.");
            if (request.PaidAt == default || request.PaidAt > now) throw new ValidationException("Provide a non-future payment timestamp.");
            var reference = Required(request.Reference, 200, "reference");
            var bills = EvidenceImages.Encode(EvidenceImages.Decode(request.Bills));
            if (bills.Length == 0) throw new ValidationException("Attach evidence of the external payment.");
            p.Payment = new() { PaidAmountVnd = request.PaidAmountVnd, PaidAt = request.PaidAt.ToUniversalTime(), Reference = reference,
                Bill = new() { ImagesJson = JsonSerializer.Serialize(bills) }, VerifiedAt = now, VerifiedBy = Actor };
        }, ct);

    public Task<SiteVisitState> ScheduleAsync(int id, SiteVisitScheduleRequest request, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Administrator, (lead, revisions, now) =>
        {
            var p = Latest(revisions);
            if (p.Channel != "ON_SITE" || p.ReviewStatus != "APPROVED" || p.ScheduledAt != null || (p.AmountVnd > 0 && p.Payment == null))
                throw new ConflictException("An approved, paid or free, unscheduled visit is required.");
            if (p.ProposedAt <= now) throw new ConflictException("The proposed visit time has passed.");
            p.ScheduledBy = Actor;
            p.ScheduledAt = now;
        }, ct);

    private Task<SiteVisitState> Change(int id, int? expected, VisitAccess access, Action<Consultation,List<SiteVisitProposal>,DateTimeOffset> apply, CancellationToken ct) =>
        repository.WithVisitAsync(id, Actor, access, true, (lead, revisions, admin) =>
        {
            if (expected is null or < 0) throw new ValidationException("A non-negative expectedRevision is required.");
            if (expected != (revisions.LastOrDefault()?.Revision ?? 0)) throw new ConflictException("Reload the current site visit revision.");
            var now = clock.GetUtcNow();
            apply(lead, revisions, now);
            lead.UpdatedAt = now;
            return State(lead, revisions, admin);
        }, ct);

    private SiteVisitState State(Consultation lead, List<SiteVisitProposal> revisions, bool admin)
    {
        var history = revisions.Select(p =>
        {
            var payment = p.Payment is not {} r ? null : new SiteVisitPaymentView(r.PaidAmountVnd, r.PaidAt, r.Reference, r.VerifiedBy,
                r.VerifiedAt, admin && r.Bill != null ? JsonSerializer.Deserialize<EvidenceImageData[]>(r.Bill.ImagesJson) : null);
            return new SiteVisitSnapshot(p.Revision, p.Channel, p.OfficeCode, p.OfficeLabel, p.DistanceKm, p.MapsUrl, p.ProposedAt,
                p.AmountVnd, p.PricingMethod, p.PricingVersion, p.ManualReason, p.ChangeReason, p.CreatedBy, p.CreatedAt, p.ReviewStatus,
                p.ReviewReason, p.ReviewedBy, p.ReviewedAt, p.AmountVnd == 0 ? "NOT_REQUIRED" : payment == null ? "UNPAID" : "CONFIRMED",
                payment, p.ScheduledBy, p.ScheduledAt);
        }).ToArray();
        return new(lead.Channel, history.LastOrDefault()?.Revision ?? 0, history.LastOrDefault(), history.SkipLast(1).ToArray(), pricing.Offices);
    }

    private static SiteVisitProposal Latest(List<SiteVisitProposal> rows) => rows.LastOrDefault() ?? throw new ConflictException("No proposal exists.");
    private static string Required(string? value,int limit,string field) => Optional(value, limit) ?? throw new ValidationException($"{field} is required.");
    private static string? Optional(string? value,int limit)
    {
        if (value?.Length > limit || value?.Contains('\0') == true) throw new ValidationException("Invalid text or length.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
