using FarmMonitoring.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class EmailDeliveryRepository(AppDbContext db) : IEmailDeliveryRepository
{
    public async Task<bool> ProcessNextAsync(Func<Domain.Entities.Notification, Task> deliver, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Keep ownership until delivery result is saved; another worker skips this row.
        var rows = await db.Notifications.FromSqlRaw("SELECT * FROM notifications WHERE channel = 'EMAIL' AND status = 'PENDING' ORDER BY id LIMIT 1 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        var notification = rows.SingleOrDefault();
        if (notification is null) return false;
        await db.Entry(notification).Reference(x => x.User).LoadAsync(ct);
        await db.Entry(notification.User).ReloadAsync(ct);
        await deliver(notification);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
