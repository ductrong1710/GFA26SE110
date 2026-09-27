using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IAlertRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task LockSubjectAsync(AlertSignal signal, CancellationToken ct);
    Task<Alert?> FindActiveAsync(AlertSignal signal, CancellationToken ct);
    Task<Alert?> FindAsync(int id, bool forUpdate, CancellationToken ct);
    Task<SensorThreshold?> ThresholdAsync(int channelId, CancellationToken ct);
    Task<int[]> RecipientsAsync(CancellationToken ct);
    void Add(Alert alert);
    void AddNotification(Notification notification);
    Task SaveAsync(CancellationToken ct);
    Task<PagedResult<AlertResponse>> ListAsync(AlertQuery query, CancellationToken ct);
    Task<PagedResult<NotificationResponse>> NotificationsAsync(int userId, NotificationQuery query, CancellationToken ct);
    Task<Notification?> ReadNotificationAsync(int userId, int id, CancellationToken ct);
}
