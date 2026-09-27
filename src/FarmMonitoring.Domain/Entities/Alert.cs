namespace FarmMonitoring.Domain.Entities;

public enum AlertStatus { OPEN, ACKNOWLEDGED, CLOSED }
public enum AlertType { SENSOR_THRESHOLD, SENSOR_DATA_TIMEOUT, SENSOR_LOW_BATTERY, GATEWAY_ERROR, GATEWAY_OFFLINE, UAV_LOW_BATTERY, MISSION_ERROR }
public enum AlertSeverity { INFO, WARNING, CRITICAL }
public enum AlertAction { CREATED, ACKNOWLEDGED, NOTE_ADDED, CLOSED, REOPENED }
public sealed class Alert
{
    public int Id { get; set; }
    public AlertType AlertType { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public int? SensorNodeId { get; set; }
    public SensorNode? SensorNode { get; set; }
    public int? SensorChannelId { get; set; }
    public SensorChannel? SensorChannel { get; set; }
    public int? GatewayId { get; set; }
    public Gateway? Gateway { get; set; }
    public int? UavId { get; set; }
    public Uav? Uav { get; set; }
    public int? MissionId { get; set; }
    public Mission? Mission { get; set; }
    public string Message { get; set; } = "";
    public decimal? TriggeredValue { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public List<AlertHistory> History { get; set; } = [];
}
public sealed class AlertHistory
{
    public int Id { get; set; }
    public int AlertId { get; set; }
    public Alert Alert { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public AlertAction Action { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public enum NotificationChannel { WEB, EMAIL }
public enum NotificationStatus { UNREAD, READ, PENDING, SENT, FAILED }
public sealed class Notification
{
    public int Id { get; set; }
    public int? AlertId { get; set; }
    public Alert? Alert { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public NotificationChannel Channel { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = "";
    public NotificationStatus Status { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
