using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Alerts;

public sealed class AlertService(FarmAccessService access, IAlertRepository repository, IValidator<AlertQuery> queries, IValidator<PageQuery> pages, TimeProvider clock, EmailDeliverySettings emailSettings)
{
    // Called inside the caller's transaction: readings, alerts, histories and web notifications commit together.
    public async Task EvaluateReadingAsync(SensorReading reading, CancellationToken ct)
    {
        if (!reading.IsValid) return;
        var threshold = await repository.ThresholdAsync(reading.SensorChannelId, ct);
        if (threshold is not { IsEnabled: true } || !threshold.SensorChannel.IsActive || !threshold.SensorChannel.SensorNode.IsActive) return;
        if ((threshold.MinValue.HasValue && reading.Value < threshold.MinValue) || (threshold.MaxValue.HasValue && reading.Value > threshold.MaxValue))
            await RaiseAsync(new(AlertType.SENSOR_THRESHOLD, AlertSeverity.WARNING, threshold.SensorChannel.SensorNodeId,
                reading.SensorChannelId, reading.GatewayId, null, reading.MissionId, "Sensor value is outside the configured threshold.", reading.Value), ct);
    }
    public async Task RaiseAsync(AlertSignal signal, CancellationToken ct)
    {
        if (signal.Type == AlertType.GATEWAY_ERROR && !signal.GatewayId.HasValue) throw new BusinessRuleException("Gateway errors require a gateway reference.");
        await repository.LockSubjectAsync(signal, ct);
        if (await repository.FindActiveAsync(signal, ct) is not null) return;
        var now = clock.GetUtcNow();
        var alert = new Alert { AlertType = signal.Type, Severity = signal.Severity, SensorNodeId = signal.SensorNodeId,
            SensorChannelId = signal.SensorChannelId, GatewayId = signal.GatewayId, UavId = signal.UavId, MissionId = signal.MissionId,
            Message = signal.Message, TriggeredValue = signal.Value, OpenedAt = now,
            History = [new AlertHistory { Action = AlertAction.CREATED, Note = signal.Message, CreatedAt = now }] };
        repository.Add(alert);
        foreach (var userId in await repository.RecipientsAsync(signal, ct))
        {
            repository.AddNotification(new Notification { Alert = alert, UserId = userId, Channel = NotificationChannel.WEB,
                Subject = signal.Type.ToString(), Message = signal.Message, Status = NotificationStatus.UNREAD, CreatedAt = now, SentAt = now });
            if (emailSettings.Enabled)
                repository.AddNotification(new Notification { Alert = alert, UserId = userId, Channel = NotificationChannel.EMAIL,
                    Subject = signal.Type.ToString(), Message = signal.Message, Status = NotificationStatus.PENDING, CreatedAt = now });
        }
    }
    public async Task<PagedResult<AlertResponse>> ListAsync(AlertQuery query, CancellationToken ct)
    {
        await queries.ValidateAndThrowAsync(query, ct);
        await access.CheckFilterAsync(FarmResource.Farm, query.FarmId, ct);
        await access.CheckFilterAsync(FarmResource.Zone, query.ZoneId, ct);
        await access.CheckFilterAsync(FarmResource.SensorNode, query.SensorNodeId, ct);
        await access.CheckFilterAsync(FarmResource.Mission, query.MissionId, ct);
        return await repository.ListAsync(query, await access.GetScopeAsync(ct), ct);
    }
    public async Task<AlertResponse> GetAsync(int id, CancellationToken ct)
    {
        await access.EnsureAsync(FarmResource.Alert, id, ct);
        return Map(await repository.FindAsync(id, false, ct) ?? throw new NotFoundException("Alert not found."));
    }
    public async Task<AlertResponse> HandleAsync(int id, AlertAction action, AlertNoteRequest request, int userId, CancellationToken ct)
    {
        if (request.Note?.Length > 10000 || request.Note?.Contains('\0') == true || (action == AlertAction.NOTE_ADDED && string.IsNullOrWhiteSpace(request.Note)))
            throw new ValidationException("Provide a valid note of at most 10000 characters.");
        return await repository.InTransactionAsync(async () =>
        {
            await access.EnsureAsync(FarmResource.Alert, id, ct);
            var alert = await repository.FindAsync(id, true, ct) ?? throw new NotFoundException("Alert not found.");
            var now = clock.GetUtcNow();
            if (action == AlertAction.ACKNOWLEDGED)
            {
                if (alert.Status != AlertStatus.OPEN) throw new ConflictException("Only open alerts can be acknowledged.");
                alert.Status = AlertStatus.ACKNOWLEDGED;
                alert.AcknowledgedAt = now;
            }
            else if (action == AlertAction.CLOSED)
            {
                if (alert.Status == AlertStatus.CLOSED) throw new ConflictException("Alert is already closed.");
                alert.Status = AlertStatus.CLOSED;
                alert.ClosedAt = now;
            }
            else if (action != AlertAction.NOTE_ADDED) throw new ValidationException("Unsupported alert action.");
            alert.History.Add(new AlertHistory { UserId = userId, Action = action, Note = request.Note?.Trim(), CreatedAt = now });
            await repository.SaveAsync(ct);
            return Map(alert);
        }, ct);
    }
    public async Task<PagedResult<NotificationResponse>> NotificationsAsync(int userId, NotificationQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        if (query.AlertId <= 0) throw new ValidationException("Invalid alert ID.");
        return await repository.NotificationsAsync(userId, query, ct);
    }
    public async Task<NotificationResponse> ReadAsync(int userId, int id, CancellationToken ct)
    {
        var notification = await repository.ReadNotificationAsync(userId, id, ct) ?? throw new NotFoundException("Notification not found.");
        return new(notification.Id, notification.AlertId, notification.Channel.ToString(), notification.Subject, notification.Message,
            notification.Status.ToString(), notification.SentAt, notification.CreatedAt);
    }
    public static AlertResponse Map(Alert x) => new(x.Id, x.AlertType.ToString(), x.Severity.ToString(), x.Status.ToString(), x.SensorNodeId,
        x.SensorChannelId, x.GatewayId, x.UavId, x.MissionId, x.Message, x.TriggeredValue, x.OpenedAt, x.AcknowledgedAt, x.ClosedAt,
        x.History.OrderBy(h => h.Id).Select(h => new AlertHistoryResponse(h.Id, h.UserId, h.Action.ToString(), h.Note, h.CreatedAt)).ToArray());
}
