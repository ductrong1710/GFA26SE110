using System.Text.Json;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Sync;

public sealed record SyncRecordRequest(string? SensorNodeCode, string? ChannelCode, string? SourceRecordKey,
    JsonElement? Value, string? MeasuredAt, string? CollectedAt, string? QualityStatus);
public sealed record CollectionResultRequest(int SensorNodeId, string? Status, int AttemptNo, int RecordsReceived,
    DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, string? ErrorCode, string? ErrorMessage);
public sealed record UnresolvedRecordRequest(string? SensorNodeCode, string? SourceRecordKey, JsonElement? Payload);
public sealed record SyncRequest(string BatchKey, int? MissionId, SyncRecordRequest?[] Records, CollectionResultRequest?[]? CollectionResults,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] UnresolvedRecordRequest?[]? UnresolvedRecords = null);
public sealed record SyncRecordOutcome(int Index, string? SourceRecordKey, string Status, string? Error);
public sealed record CollectionOutcome(int Index, int? SensorNodeId, int? AttemptNo, string Status, string? Error);
public sealed record SyncResponse(string BatchKey, int Accepted, int Duplicates, int Rejected,
    IReadOnlyList<SyncRecordOutcome> Records, IReadOnlyList<CollectionOutcome> CollectionResults, IReadOnlyList<SyncRecordOutcome>? UnresolvedRecords = null);
public sealed class SyncValidator : AbstractValidator<SyncRequest>
{
    public SyncValidator()
    {
        RuleFor(x => x.BatchKey).NotEmpty().MaximumLength(150).Must(x => x is null || !x.Contains('\0')).WithMessage("Batch key contains an unsupported character.");
        RuleFor(x => x.MissionId).GreaterThan(0);
        RuleFor(x => x.Records).NotNull().Must(x => x is null || x.Length <= 1000).WithMessage("At most 1000 records per batch.");
        RuleFor(x => x.UnresolvedRecords).Must(x => x is null || x.Length <= 100).WithMessage("At most 100 unresolved records per batch.");
        RuleFor(x => x.CollectionResults).Must(x => x is null || x.Length <= 1000).WithMessage("At most 1000 collection results per batch.");
        RuleFor(x => x).Must(x => x.MissionId.HasValue || x.CollectionResults is null || x.CollectionResults.Length == 0)
            .WithMessage("Collection results require a mission.");
    }
}
