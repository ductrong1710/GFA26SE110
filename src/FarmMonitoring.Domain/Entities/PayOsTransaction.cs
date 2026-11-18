namespace FarmMonitoring.Domain.Entities;

public sealed class PayOsTransaction
{
    public long Id { get; set; }
    public long OrderCode { get; set; }
    public string? PublicStatusToken { get; set; }
    public string? PaymentLinkId { get; set; }
    public int ProposalId { get; set; }
    public long AmountVnd { get; set; }
    public string CheckoutUrl { get; set; } = "";
    public string? QrCode { get; set; }
    public string Status { get; set; } = "CREATING";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? ProviderReference { get; set; }
    public string? ProviderPaymentLinkId { get; set; }
    public string? ProviderCode { get; set; }
    public long? ProviderAmountVnd { get; set; }
    public string? ProviderTransactionDateTime { get; set; }
    public int CreatedBy { get; set; }
    public SiteVisitProposal Proposal { get; set; } = null!;
}
