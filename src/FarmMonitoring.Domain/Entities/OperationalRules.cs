namespace FarmMonitoring.Domain.Entities;

public static class OperationalRules
{
    public static readonly TimeSpan TravelBuffer = TimeSpan.FromMinutes(30);
    public static DateTimeOffset RentalStart(DateTimeOffset completedAt)
    {
        var local = completedAt.ToOffset(TimeSpan.FromHours(7));
        return new DateTimeOffset(local.Date.AddDays(1), TimeSpan.FromHours(7)).ToUniversalTime();
    }
    public static bool Overlaps(DateTimeOffset start, DateTimeOffset end, DateTimeOffset otherStart, DateTimeOffset otherEnd) =>
        start < otherEnd + TravelBuffer && end + TravelBuffer > otherStart;
}
