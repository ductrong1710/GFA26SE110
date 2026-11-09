namespace FarmMonitoring.Domain.Entities;
public sealed class PublicPriceVersion
{
    public int Id { get; set; }
    public int PublicContentId { get; set; }
    public int Revision { get; set; }
    public string Title { get; set; } = "";
    public decimal? MonthlyRentVnd { get; set; }
    public decimal? ReplacementPriceVnd { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public bool Published { get; set; }
}
