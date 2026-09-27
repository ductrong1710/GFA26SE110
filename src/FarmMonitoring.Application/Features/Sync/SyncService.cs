using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Application.Features.Alerts;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Sync;

public sealed class SyncService(ISyncRepository repository, IMissionRepository missions, IValidator<SyncRequest> validator, TimeProvider clock, AlertService alerts)
{
    public async Task<SyncResponse> SynchronizeAsync(int gatewayId, int authenticatedGateway, SyncRequest request, CancellationToken ct)
    {
        if (gatewayId != authenticatedGateway) throw new AccessDeniedException("Gateway identity does not match the route.");
        await validator.ValidateAndThrowAsync(request, ct);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        return await missions.InTransactionAsync(async () =>
        {
            Mission? mission = null;
            if (request.MissionId is int missionId)
            {
                mission = await missions.FindAsync(missionId, true, ct) ?? throw new NotFoundException("Mission not found.");
                if (mission.GatewayId != gatewayId) throw new AccessDeniedException("Gateway is not assigned to this mission.");
                if (mission.UavId is int uavId) await missions.LockUavAsync(uavId, ct);
            }
            var gateway = await missions.LockGatewayAsync(gatewayId, ct);
            if (gateway is not { IsActive: true }) throw new AccessDeniedException("Gateway is inactive.");
            var previous = await repository.FindBatchAsync(gatewayId, request.BatchKey, ct);
            if (previous is not null)
            {
                if (previous.PayloadHash != hash) throw new ConflictException("Batch key was already used for a different payload.");
                gateway.LastSeenAt = clock.GetUtcNow();
                await repository.SaveAsync(ct);
                return JsonSerializer.Deserialize<SyncResponse>(previous.ResponseJson)!;
            }
            if (mission is { Status: MissionStatus.PENDING or MissionStatus.SCHEDULED })
                throw new BusinessRuleException("Collection requires a mission that has started.");
            var now = clock.GetUtcNow();
            gateway.LastSeenAt = now;
            var channels = await repository.LockChannelsAsync(request.Records.Where(x => x?.SensorNodeCode is { Length: <= 100 } code && SafeText(code)).Select(x => x!.SensorNodeCode!).Distinct().ToArray(), ct);
            var outcomes = new List<SyncRecordOutcome>();
            for (var index = 0; index < request.Records.Length; index++)
                outcomes.Add(await ProcessRecord(index, request.Records[index], channels, mission, gatewayId, now, ct));
            var attempts = mission is null ? [] : (await repository.AttemptsAsync(mission.Id, ct)).ToList();
            var collection = (request.CollectionResults ?? []).Select((item, index) => ProcessAttempt(index, item, mission!, gatewayId, now, attempts)).ToArray();
            var response = new SyncResponse(request.BatchKey, outcomes.Count(x => x.Status == SyncOutcomeStatuses.Accepted), outcomes.Count(x => x.Status == SyncOutcomeStatuses.Duplicate),
                outcomes.Count(x => x.Status == SyncOutcomeStatuses.Rejected), outcomes, collection);
            repository.AddBatch(new SyncBatch
            {
                GatewayId = gatewayId, MissionId = request.MissionId, BatchKey = request.BatchKey, PayloadHash = hash,
                RecordCount = outcomes.Count, AcceptedCount = response.Accepted, DuplicateCount = response.Duplicates, RejectedCount = response.Rejected,
                Status = response.Rejected == 0 && collection.All(x => x.Status != SyncOutcomeStatuses.Rejected) ? SyncBatchStatuses.Completed : SyncBatchStatuses.Partial,
                StartedAt = now, CompletedAt = clock.GetUtcNow(), ResponseJson = JsonSerializer.Serialize(response)
            });
            if (mission is not null && collection.Any(x => x.Status == SyncOutcomeStatuses.Accepted))
                missions.AddLog(new MissionLog { MissionId = mission.Id, GatewayId = gatewayId, LogType = "SYNC", Message = $"Batch {request.BatchKey}: {response.Accepted} readings accepted.", CreatedAt = now });
            await repository.SaveAsync(ct);
            return response;
        }, ct);
    }

    private async Task<SyncRecordOutcome> ProcessRecord(int index, SyncRecordRequest? request, IReadOnlyList<SensorChannel> channels,
        Mission? mission, int gatewayId, DateTimeOffset now, CancellationToken ct)
    {
        SyncRecordOutcome Rejected(string error) => new(index, SafeText(request?.SourceRecordKey) ? request?.SourceRecordKey : null, SyncOutcomeStatuses.Rejected, error);
        if (request is null) return Rejected("Record is required.");
        if (!SafeText(request.SensorNodeCode) || !SafeText(request.ChannelCode) || !SafeText(request.SourceRecordKey) || !SafeText(request.QualityStatus))
            return Rejected("Record contains an unsupported text character.");
        if (string.IsNullOrWhiteSpace(request.SensorNodeCode) || request.SensorNodeCode.Length > 100 || string.IsNullOrWhiteSpace(request.ChannelCode) || request.ChannelCode.Length > 100
            || string.IsNullOrWhiteSpace(request.SourceRecordKey) || request.SourceRecordKey.Length > 150) return Rejected("Valid sensor, channel and source record keys are required.");
        if (request.Value is not { ValueKind: JsonValueKind.Number } number || !number.TryGetDecimal(out var value) || value is < -999999999999.999999m or > 999999999999.999999m)
            return Rejected("Value must be a number within the supported storage range.");
        if (!ParseTime(request.MeasuredAt, out var measured) || !ParseTime(request.CollectedAt, out var collected) || measured > collected || collected > now.AddMinutes(5))
            return Rejected("Valid measurement and collection timestamps in chronological order are required.");
        if (request.QualityStatus?.Length > 50) return Rejected("Quality status is too long.");
        var channel = channels.SingleOrDefault(x => x.SensorNode.DeviceCode == request.SensorNodeCode && x.ChannelCode == request.ChannelCode);
        if (channel is null) return Rejected("Unknown sensor or channel.");
        if (!channel.IsActive || !channel.SensorNode.IsActive) return Rejected("Sensor and channel must be active.");
        if (mission is not null && (channel.SensorNode.Zone.FarmId != mission.FarmId || !mission.Targets.Any(x => x.SensorNodeId == channel.SensorNodeId)))
            return Rejected("Sensor is not a target in this mission farm.");
        value = decimal.Round(value, 6, MidpointRounding.AwayFromZero);
        var existing = await repository.FindReadingAsync(channel.Id, request.SourceRecordKey, ct);
        if (existing is not null)
            return existing.Value == value && existing.MeasuredAt == measured && existing.CollectedAt == collected && existing.MissionId == mission?.Id
                && existing.QualityStatus == request.QualityStatus
                ? new(index, request.SourceRecordKey, SyncOutcomeStatuses.Duplicate, null) : Rejected("Source record key already identifies different data.");
        var reading = new SensorReading
        {
            SensorChannelId = channel.Id, GatewayId = gatewayId, MissionId = mission?.Id, SourceRecordKey = request.SourceRecordKey,
            Value = value, MeasuredAt = measured, CollectedAt = collected, ReceivedAt = now, QualityStatus = request.QualityStatus
        };
        repository.AddReading(reading);
        await alerts.EvaluateReadingAsync(reading, ct);
        return new(index, request.SourceRecordKey, SyncOutcomeStatuses.Accepted, null);
    }

    private CollectionOutcome ProcessAttempt(int index, CollectionResultRequest? request, Mission mission, int gatewayId,
        DateTimeOffset now, List<CollectionAttempt> attempts)
    {
        CollectionOutcome Rejected(string error) => new(index, request?.SensorNodeId, request?.AttemptNo, SyncOutcomeStatuses.Rejected, error);
        if (request is null) return Rejected("Collection result is required.");
        if (request.AttemptNo <= 0 || request.RecordsReceived < 0 || request.Status is not (CollectionStatuses.Success or CollectionStatuses.Failed or CollectionStatuses.Timeout or CollectionStatuses.Skipped)
            || request.ErrorCode?.Length > 100 || request.ErrorMessage?.Length > 500 || !SafeText(request.ErrorCode) || !SafeText(request.ErrorMessage)) return Rejected("Invalid collection outcome.");
        var target = mission.Targets.SingleOrDefault(x => x.SensorNodeId == request.SensorNodeId);
        if (target is null) return Rejected("Sensor is not a mission target.");
        var started = Normalize(request.StartedAt ?? request.FinishedAt ?? now);
        var finished = Normalize(request.FinishedAt ?? request.StartedAt ?? now);
        if (finished < started || finished > now.AddMinutes(5)) return Rejected("Invalid attempt timestamps.");
        var previous = attempts.SingleOrDefault(x => x.MissionTargetId == target.Id && x.AttemptNo == request.AttemptNo);
        if (previous is not null)
            return previous.Status == request.Status && previous.RecordsReceived == request.RecordsReceived && previous.ErrorCode == request.ErrorCode && previous.ErrorMessage == request.ErrorMessage
                && (!request.StartedAt.HasValue || previous.StartedAt == started) && (!request.FinishedAt.HasValue || previous.FinishedAt == finished)
                ? new(index, request.SensorNodeId, request.AttemptNo, SyncOutcomeStatuses.Duplicate, null) : Rejected("Attempt number already identifies different data.");
        var attempt = new CollectionAttempt { MissionId = mission.Id, MissionTargetId = target.Id, GatewayId = gatewayId, AttemptNo = request.AttemptNo,
            StartedAt = started, FinishedAt = finished, Status = request.Status, RecordsReceived = request.RecordsReceived, ErrorCode = request.ErrorCode, ErrorMessage = request.ErrorMessage };
        repository.AddAttempt(attempt);
        attempts.Add(attempt);
        if (target.Status != MissionTargetStatus.COLLECTED && (request.Status == CollectionStatuses.Success || !attempts.Any(x => x.MissionTargetId == target.Id && x.AttemptNo > request.AttemptNo)))
            target.Status = request.Status switch { CollectionStatuses.Success => MissionTargetStatus.COLLECTED, CollectionStatuses.Skipped => MissionTargetStatus.SKIPPED, _ => MissionTargetStatus.FAILED };
        return new(index, request.SensorNodeId, request.AttemptNo, SyncOutcomeStatuses.Accepted, null);
    }
    private static bool ParseTime(string? text, out DateTimeOffset value)
    {
        value = default;
        if (text is null || !text.Contains('T') || !(text.EndsWith('Z') || (text.Length >= 6 && text[^3] == ':' && text[^6] is '+' or '-'))
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        value = Normalize(parsed);
        return true;
    }
    private static DateTimeOffset Normalize(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    private static bool SafeText(string? value) => value is null || !value.Contains('\0');
}
