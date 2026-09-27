using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.Application.Features.SensorData;

public sealed class ReadingQuery : PageQuery
{
    public int? SensorNodeId { get; init; }
    public int? SensorChannelId { get; init; }
    public int? FarmId { get; init; }
    public int? ZoneId { get; init; }
    public int? MissionId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}
public sealed record ReadingResponse(long Id, int SensorChannelId, int SensorNodeId, string ChannelCode, string? Unit,
    int? GatewayId, int? MissionId, string SourceRecordKey, decimal Value, DateTimeOffset MeasuredAt,
    DateTimeOffset CollectedAt, DateTimeOffset ReceivedAt, string? QualityStatus, bool IsValid, string? ValidationError);
public sealed record LatestChannelResponse(int SensorChannelId, string ChannelCode, string? Unit, ReadingResponse? Reading);
public sealed record CollectionAttemptResponse(int Id, int MissionId, int MissionTargetId, int? GatewayId, int AttemptNo,
    DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, int RecordsReceived, string? ErrorCode, string? ErrorMessage);
public sealed class ZoneComparisonQuery
{
    public string ZoneIds { get; init; } = "";
    public int SensorTypeId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}
public sealed record ZoneComparisonResponse(int ZoneId, string ZoneName, int SensorTypeId, string? Unit,
    DateTimeOffset From, DateTimeOffset To, long ReadingCount, decimal? Minimum, decimal? Maximum, decimal? Average);
public sealed class ReadingQueryValidator : AbstractValidator<ReadingQuery>
{
    public ReadingQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.SensorNodeId).GreaterThan(0);
        RuleFor(x => x.SensorChannelId).GreaterThan(0);
        RuleFor(x => x.FarmId).GreaterThan(0);
        RuleFor(x => x.ZoneId).GreaterThan(0);
        RuleFor(x => x.MissionId).GreaterThan(0);
        RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.To >= x.From).WithMessage("Invalid reading date range.");
    }
}
