namespace FarmMonitoring.Domain.Entities;

public enum PublicContentKind { SERVICE, DEVICE, INSTALLATION, WARRANTY, MAINTENANCE, RETURN, CONTACT, COMPONENT, POLICY }

public sealed class PublicContent
{
    public int Id { get; set; }
    public PublicContentKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public decimal? EstimatedMonthlyRentVnd { get; set; }
    public decimal? ReplacementPriceVnd { get; set; }
    public string? ImageType { get; set; }
    public byte[]? Image { get; set; }
    public bool Published { get; set; }
    public int Revision { get; set; } = 1;
}
