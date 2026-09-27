using FarmMonitoring.Application.Common;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Alerts;

public sealed class AlertQuery : PageQuery
{
    public string? Status { get; init; }
    public string? Severity { get; init; }
    public string? AlertType { get; init; }
    public int? FarmId { get; init; }
    public int? ZoneId { get; init; }
    public int? SensorNodeId { get; init; }
    public int? GatewayId { get; init; }
    public int? MissionId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}
public sealed record AlertNoteRequest(string? Note);
public sealed record AlertHistoryResponse(int Id, int? UserId, string Action, string? Note, DateTimeOffset CreatedAt);
public sealed record AlertResponse(int Id, string AlertType, string Severity, string Status, int? SensorNodeId, int? SensorChannelId,
    int? GatewayId, int? UavId, int? MissionId, string Message, decimal? TriggeredValue, DateTimeOffset OpenedAt,
    DateTimeOffset? AcknowledgedAt, DateTimeOffset? ClosedAt, IReadOnlyList<AlertHistoryResponse>? History);
public sealed class NotificationQuery : PageQuery
{
    public int? AlertId { get; init; }
}
public sealed record NotificationResponse(int Id, int? AlertId, string Channel, string? Subject, string Message, string Status, DateTimeOffset? SentAt, DateTimeOffset CreatedAt);
public sealed record AlertSignal(AlertType Type, AlertSeverity Severity, int? SensorNodeId, int? SensorChannelId,
    int? GatewayId, int? UavId, int? MissionId, string Message, decimal? Value);

public sealed class AlertQueryValidator : AbstractValidator<AlertQuery>
{
    public AlertQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).Must(x => x is null || Enum.GetNames<AlertStatus>().Contains(x)).WithMessage("Invalid alert status.");
        RuleFor(x => x.Severity).Must(x => x is null || Enum.GetNames<AlertSeverity>().Contains(x)).WithMessage("Invalid severity.");
        RuleFor(x => x.AlertType).Must(x => x is null || Enum.GetNames<Domain.Entities.AlertType>().Contains(x)).WithMessage("Invalid alert type.");
        RuleFor(x => x.FarmId).GreaterThan(0);
        RuleFor(x => x.ZoneId).GreaterThan(0);
        RuleFor(x => x.SensorNodeId).GreaterThan(0);
        RuleFor(x => x.GatewayId).GreaterThan(0);
        RuleFor(x => x.MissionId).GreaterThan(0);
        RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.To >= x.From).WithMessage("Invalid date range.");
    }
}
