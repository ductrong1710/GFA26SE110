using FarmMonitoring.Application.Common;
using System.Text.Json.Serialization;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed record SiteVisitChannelRequest(string Channel, int? ExpectedRevision);
public sealed record SiteVisitPreviewRequest(string OfficeCode, [property: JsonConverter(typeof(SiteVisitDistanceConverter))] decimal DistanceKm);
public sealed record SiteVisitProposeRequest(int? ExpectedRevision, string? OfficeCode = null,
    [property: JsonConverter(typeof(SiteVisitDistanceConverter))] decimal? DistanceKm = null,
    string? MapsUrl = null, DateTimeOffset? ProposedAt = null, string? PricingVersion = null, long? ManualAmountVnd = null,
    string? ManualReason = null, string? Channel = null, string? Reason = null);
public sealed record SiteVisitReviewRequest(int? ExpectedRevision, string Decision, string? Reason = null);
public sealed record SiteVisitConfirmRequest(int? ExpectedRevision, long PaidAmountVnd, DateTimeOffset PaidAt, string Reference, EvidenceImageData[] Bills);
public sealed record SiteVisitScheduleRequest(int? ExpectedRevision);
public sealed record SiteVisitPaymentView(long PaidAmountVnd, DateTimeOffset PaidAt, string Reference, int VerifiedBy, DateTimeOffset VerifiedAt, EvidenceImageData[]? Bills);
public sealed record SiteVisitSnapshot(int Revision, string Channel, string? OfficeCode, string? OfficeLabel, decimal? DistanceKm,
    string? MapsUrl, DateTimeOffset? ProposedAt, long AmountVnd, string PricingMethod, string? PricingVersion, string? ManualReason,
    string? ChangeReason, int CreatedBy, DateTimeOffset CreatedAt, string ReviewStatus, string? ReviewReason, int? ReviewedBy,
    DateTimeOffset? ReviewedAt, string PaymentStatus, SiteVisitPaymentView? Payment, int? ScheduledBy, DateTimeOffset? ScheduledAt);
public sealed record SiteVisitState(string? Channel, int Revision, SiteVisitSnapshot? Current, SiteVisitSnapshot[] History, IReadOnlyList<SiteVisitOffice> Offices);
