namespace FarmMonitoring.Domain.Entities;

public sealed class SiteVisitProposal
{
    public int Id { get; set; }
    public int ConsultationId { get; set; }
    public int Revision { get; set; }
    public string Channel { get; set; } = "ON_SITE";
    public string? OfficeCode { get; set; }
    public string? OfficeLabel { get; set; }
    public decimal? DistanceKm { get; set; }
    public string? MapsUrl { get; set; }
    public DateTimeOffset? ProposedAt { get; set; }
    public long AmountVnd { get; set; }
    public string PricingMethod { get; set; } = "";
    public string? PricingVersion { get; set; }
    public string? ManualReason { get; set; }
    public string? ChangeReason { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string ReviewStatus { get; set; } = "PENDING";
    public string? ReviewReason { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public int? ScheduledBy { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public SiteVisitPayment? Payment { get; set; }
}

public sealed class SiteVisitPayment
{
    public int ProposalId { get; set; }
    public long PaidAmountVnd { get; set; }
    public DateTimeOffset PaidAt { get; set; }
    public string Reference { get; set; } = "";
    public DateTimeOffset VerifiedAt { get; set; }
    public int VerifiedBy { get; set; }
    public SiteVisitBill? Bill { get; set; }
}

// Kept out of the ordinary proposal/history query; only administrators can read bills.
public sealed class SiteVisitBill
{
    public int ProposalId { get; set; }
    public string ImagesJson { get; set; } = "[]";
}
