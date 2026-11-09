using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

internal static class WorkflowNotifications
{
    public static async Task AddForUserAsync(AppDbContext db, int userId, string subject, string message, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(x => x.Id == userId && x.IsActive, ct)) Add(db, userId, subject, message, now);
    }
    public static async Task AddForRoleAsync(AppDbContext db, string role, string subject, string message, DateTimeOffset now, CancellationToken ct)
    {
        var ids = await db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == role)).Select(x => x.Id).ToArrayAsync(ct);
        foreach (var id in ids) Add(db, id, subject, message, now);
    }
    private static void Add(AppDbContext db, int userId, string subject, string message, DateTimeOffset now)
    {
        foreach (var channel in new[] { NotificationChannel.WEB, NotificationChannel.EMAIL })
            db.Notifications.Add(new() { UserId = userId, Channel = channel, Subject = subject, Message = message,
                Status = channel == NotificationChannel.WEB ? NotificationStatus.UNREAD : NotificationStatus.PENDING, CreatedAt = now });
    }
}
