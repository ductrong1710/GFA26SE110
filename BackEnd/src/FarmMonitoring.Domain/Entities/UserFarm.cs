namespace FarmMonitoring.Domain.Entities;

public sealed class UserFarm
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int FarmId { get; set; }
    public Farm Farm { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
