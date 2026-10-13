namespace FarmMonitoring.Domain.Entities;

public sealed class GatewayCredential
{
    public int Id { get; set; }
    public int GatewayId { get; set; }
    public Gateway Gateway { get; set; } = null!;
    public string KeyHash { get; set; } = "";
    public string? KeyPrefix { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
