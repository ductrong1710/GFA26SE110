using System.Text.Json;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed class SiteVisitService(ISiteVisitRepository repository, ICurrentUser current, TimeProvider clock, SiteVisitPricing pricing, IPayOsClient payOs)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");

    public async Task<SiteVisitState> GetAsync(int id, CancellationToken ct)
    {
        var offices = await OfficesAsync(ct);
        return await repository.WithVisitAsync(id, Actor, VisitAccess.Reader, false,
            (lead, revisions, admin) => State(lead, revisions, admin, offices), ct);
    }

    public Task<IReadOnlyList<SiteVisitOffice>> ListOfficesAsync(CancellationToken ct) => repository.ListOfficesAsync(ct);

    public Task<SiteVisitOffice> AddOfficeAsync(CreateSiteVisitOfficeRequest request, CancellationToken ct) =>
        repository.AddOfficeAsync(Actor, Required(request.Name, 120, "Tên trụ sở"), SiteVisitPricing.ValidateMapsUrl(request.MapsUrl?.Trim()), ct);

    private async Task<IReadOnlyList<SiteVisitOffice>> OfficesAsync(CancellationToken ct)
    {
        var offices = await repository.ListOfficesAsync(ct);
        return offices.Count > 0 ? offices : pricing.Offices;
    }

    public async Task<SiteVisitFeePreview> PreviewAsync(int id, SiteVisitPreviewRequest request, CancellationToken ct)
    {
        var offices = await OfficesAsync(ct);
        return await repository.WithVisitAsync(id, Actor, VisitAccess.Reader, false, (_, _, _) =>
            pricing.Preview(request.OfficeCode, request.DistanceKm, offices), ct);
    }

    public Task<SiteVisitState> ChannelAsync(int id, SiteVisitChannelRequest request, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Engineer, (lead, revisions, now, offices) =>
        {
            if (revisions.Count != 0) throw new ConflictException("Submit a new revision to change the channel.");
            if (request.Channel is not ("ONLINE" or "ON_SITE") || lead.PreferredChannel != null && request.Channel != "ONLINE") throw new ValidationException("Unknown consultation channel.");
            lead.History.Add(new() { Action = ConsultationAction.CHANNEL_CHANGED, Status = lead.Status, UserId = Actor,
                CreatedAt = now, Notes = $"{lead.Channel ?? "UNSPECIFIED"} -> {request.Channel}" });
            lead.Channel = request.Channel;
        }, ct);

    public Task<SiteVisitState> ProposeAsync(int id, SiteVisitProposeRequest request, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Engineer, (lead, revisions, now, offices) =>
        {
            if (revisions.SelectMany(x => x.PayOsTransactions).Any(PayOsService.IsUnresolved))
                throw new ConflictException("Payment outcome is unresolved. Reconcile it before submitting another proposal.");
            var previous = revisions.LastOrDefault();
            if (previous?.Payment is not null || previous?.ScheduledAt is not null)
                throw new ConflictException("Confirmed receipts and schedules require administrator reconciliation.");
            var channel = request.Channel ?? lead.Channel ?? previous?.Channel;
            if (channel is not ("ONLINE" or "ON_SITE"))
                throw new ConflictException("Hãy xác nhận hình thức tư vấn của khách trước khi gửi báo cáo.");
            var previousChoice = lead.Channel ?? lead.PreferredChannel ?? previous?.Channel;
            var reason = channel == "ON_SITE" ? Optional(request.Reason, 1000) : null;
            if (channel == "ON_SITE" && previousChoice == "ONLINE" && reason is null)
                throw new ValidationException("Nhập lý do chuyển sang tư vấn tại vườn.");
            var proposal = new SiteVisitProposal { ConsultationId = id, Revision = (previous?.Revision ?? 0) + 1, Channel = channel,
                ChangeReason = reason, CreatedBy = Actor, CreatedAt = now };
            if (channel == "ONLINE")
            {
                if (previousChoice != "ON_SITE") throw new ConflictException("Chỉ gửi báo cáo chuyển sang trực tuyến khi khách đã chọn tại vườn.");
                if (request.OfficeCode != null || request.DistanceKm != null || request.MapsUrl != null || request.ProposedAt != null
                    || request.ManualAmountVnd != null || request.ManualReason != null || request.PricingVersion != null || request.GardenAddress != null)
                    throw new ValidationException("Online adjustments cannot include travel or fee details.");
                proposal.PricingMethod = "ONLINE";
            }
            else
            {
                var fee = pricing.Preview(request.OfficeCode, request.DistanceKm ?? 0, offices);
                if (fee.Status == "PRICING_NOT_CONFIGURED") throw new BusinessRuleException("PRICING_NOT_CONFIGURED");
                if (fee.PricingVersion != request.PricingVersion || string.IsNullOrWhiteSpace(request.PricingVersion))
                    throw new ConflictException("Pricing changed. Request a fresh fee preview.");
                if (request.ProposedAt is null || request.ProposedAt <= now) throw new ValidationException("A future proposedAt is required.");
                proposal.OfficeCode = fee.OfficeCode;
                proposal.OfficeLabel = fee.OfficeLabel;
                proposal.OfficeMapsUrl = fee.OfficeMapsUrl;
                proposal.DistanceKm = fee.DistanceKm;
                proposal.GardenAddress = lead.PreferredChannel == null
                    ? Optional(request.GardenAddress, 500) : Required(request.GardenAddress, 500, "gardenAddress");
                proposal.MapsUrl = SiteVisitPricing.ValidateMapsUrl(request.MapsUrl);
                proposal.ProposedAt = request.ProposedAt.Value.ToUniversalTime();
                proposal.PricingVersion = fee.PricingVersion;
                if (request.ManualAmountVnd != null || request.ManualReason != null)
                    throw new ValidationException("Phí tư vấn tại vườn được hệ thống tính theo biểu phí.");
                proposal.AmountVnd = fee.AmountVnd ?? throw new BusinessRuleException("PRICING_NOT_CONFIGURED");
                proposal.PricingMethod = "CALCULATED";
            }
            revisions.Add(proposal);
        }, ct);

    public Task<SiteVisitState> ReviewAsync(int id, SiteVisitReviewRequest request, string ip, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Administrator, (lead, revisions, now, offices) =>
        {
            var p = Latest(revisions);
            if (p.ReviewStatus != "PENDING") throw new ConflictException("Only a pending revision can be reviewed.");
            if (request.Decision is not ("APPROVE" or "RETURN")) throw new ValidationException("Decision must be APPROVE or RETURN.");
            p.ReviewReason = request.Decision == "RETURN" ? Required(request.Reason, 1000, "reason") : Optional(request.Reason, 1000);
            p.ReviewStatus = request.Decision == "APPROVE" ? "APPROVED" : "RETURNED";
            p.ReviewedAt = now;
            p.ReviewedBy = Actor;
            if (p.ReviewStatus == "APPROVED")
            {
                lead.Channel = p.Channel;
                if (p.Channel == "ON_SITE")
                {
                    if (p.ProposedAt <= now) throw new ConflictException("Ngày giờ tư vấn đã qua. Hãy trả báo cáo để kỹ sư cập nhật.");
                    if (p.AmountVnd == 0)
                    {
                        p.ScheduledBy = Actor;
                        p.ScheduledAt = now;
                    }
                }
            }
        }, ct);

    public Task<SiteVisitState> ConfirmAsync(int id, SiteVisitConfirmRequest request, CancellationToken ct) =>
        Change(id, request.ExpectedRevision, VisitAccess.Administrator, (lead, revisions, now, offices) =>
        {
            var p = Latest(revisions);
            if (lead.PreferredChannel != null) throw new ConflictException("Yêu cầu này cần thanh toán qua cổng thanh toán trực tuyến.");
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
        Change(id, request.ExpectedRevision, VisitAccess.Administrator, (lead, revisions, now, offices) =>
        {
            if (revisions.SelectMany(x => x.PayOsTransactions).Any(PayOsService.IsUnresolved))
                throw new ConflictException("PayOS payment outcome is unresolved. Reconcile it before scheduling.");
            var p = Latest(revisions);
            if (p.Channel != "ON_SITE" || p.ReviewStatus != "APPROVED" || p.ScheduledAt != null || (p.AmountVnd > 0 && p.Payment == null))
                throw new ConflictException("An approved, paid or free, unscheduled visit is required.");
            if (p.ProposedAt <= now) throw new ConflictException("The proposed visit time has passed.");
            p.ScheduledBy = Actor;
            p.ScheduledAt = now;
        }, ct);

    private async Task<SiteVisitState> Change(int id, int? expected, VisitAccess access, Action<Consultation,List<SiteVisitProposal>,DateTimeOffset,IReadOnlyList<SiteVisitOffice>> apply, CancellationToken ct)
    {
        var offices = await OfficesAsync(ct);
        return await repository.WithVisitAsync(id, Actor, access, true, (lead, revisions, admin) =>
        {
            if (expected is null or < 0) throw new ValidationException("A non-negative expectedRevision is required.");
            if (expected != (revisions.LastOrDefault()?.Revision ?? 0)) throw new ConflictException("Reload the current site visit revision.");
            var now = clock.GetUtcNow();
            apply(lead, revisions, now, offices);
            lead.UpdatedAt = now;
            return State(lead, revisions, admin, offices);
        }, ct);

    }

    private SiteVisitState State(Consultation lead, List<SiteVisitProposal> revisions, bool admin, IReadOnlyList<SiteVisitOffice> offices)
    {
        var history = revisions.Select(p =>
        {
            var payment = p.Payment is not {} r ? null : new SiteVisitPaymentView(r.PaidAmountVnd, r.PaidAt, r.Reference, r.VerifiedBy,
                r.VerifiedAt, admin && r.Bill != null ? JsonSerializer.Deserialize<EvidenceImageData[]>(r.Bill.ImagesJson) : null, r.Source);
            return new SiteVisitSnapshot(p.Revision, p.Channel, p.OfficeCode, p.OfficeLabel, p.DistanceKm, p.MapsUrl, p.ProposedAt,
                p.AmountVnd, p.PricingMethod, p.PricingVersion, p.ManualReason, p.ChangeReason, p.CreatedBy, p.CreatedAt, p.ReviewStatus,
                p.ReviewReason, p.ReviewedBy, p.ReviewedAt, p.AmountVnd == 0 ? "NOT_REQUIRED" : payment == null ? "UNPAID" : "CONFIRMED",
                payment, p.ScheduledBy, p.ScheduledAt, p.PayOsTransactions.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).FirstOrDefault() is {} attempt
                    ? new PayOsAttemptView(attempt.OrderCode, attempt.PaymentLinkId, PayOsService.DisplayStatus(attempt, clock.GetUtcNow()), attempt.ExpiresAt,
                        attempt.Status == "PENDING" && attempt.ExpiresAt > clock.GetUtcNow() ? attempt.CheckoutUrl : null,
                        attempt.Status == "PENDING" && attempt.ExpiresAt > clock.GetUtcNow() ? attempt.QrCode : null) : null, p.GardenAddress, p.OfficeMapsUrl);
        }).ToArray();
        var latest = history.LastOrDefault();
        var unresolved = revisions.SelectMany(p => p.PayOsTransactions).Where(PayOsService.IsUnresolved).OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).FirstOrDefault();
        if (latest != null && unresolved != null)
            latest = latest with { PayOs = new PayOsAttemptView(unresolved.OrderCode, unresolved.PaymentLinkId, PayOsService.DisplayStatus(unresolved, clock.GetUtcNow()), unresolved.ExpiresAt,
                unresolved.Status == "PENDING" && unresolved.ExpiresAt > clock.GetUtcNow() ? unresolved.CheckoutUrl : null,
                unresolved.Status == "PENDING" && unresolved.ExpiresAt > clock.GetUtcNow() ? unresolved.QrCode : null) };
        return new(lead.Channel, latest?.Revision ?? 0, latest, history.SkipLast(1).ToArray(), offices, payOs.IsConfigured, pricing.Tariff);
    }

    private static SiteVisitProposal Latest(List<SiteVisitProposal> rows) => rows.LastOrDefault() ?? throw new ConflictException("No proposal exists.");
    private static string Required(string? value,int limit,string field) => Optional(value, limit) ?? throw new ValidationException($"{field} is required.");
    private static string? Optional(string? value,int limit)
    {
        if (value?.Length > limit || value?.Contains('\0') == true) throw new ValidationException("Invalid text or length.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
