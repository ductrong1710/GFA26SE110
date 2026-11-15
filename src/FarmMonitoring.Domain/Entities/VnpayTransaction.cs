namespace FarmMonitoring.Domain.Entities;

public sealed class VnpayTransaction
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public string TxnRef { get; set; } = "";
    public string TmnCode { get; set; } = "";
    public long AmountVnd { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string CheckoutUrl { get; set; } = "";
    public string PublicToken { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public string? ResponseCode { get; set; }
    public string? TransactionStatus { get; set; }
    public string? ProviderTransactionNo { get; set; }
    public DateTimeOffset? PayDate { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
