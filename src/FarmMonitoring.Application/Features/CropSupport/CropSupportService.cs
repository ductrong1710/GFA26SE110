using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Application.Features.CropSeasons;
using FluentValidation;

namespace FarmMonitoring.Application.Features.CropSupport;

public sealed record CropConditionSummary(int Id, int FarmId, int? ZoneId, string Crop, string Description,
    DateTimeOffset ReportedAt, string Status, int ReporterId, int? EngineerId, int Revision,
    int? CropSeasonId = null, DateTimeOffset? ObservedAt = null);
public sealed record SupportingCropSeason(int Id, string Name, string Crop, DateOnly? StartDate, DateOnly? EndDate, string Status);
public sealed record CropSensorReading(long Id, int SensorChannelId, string ChannelCode, string? Unit,
    decimal Value, DateTimeOffset MeasuredAt, bool IsValid, string? QualityStatus);
public sealed record CropConsultationDetail(int Id, int UserId, string Action, string Content,
    bool RecommendSiteSurvey, DateTimeOffset CreatedAt, EvidenceImageData[] Images, string? FollowUpType = null,
    int? CultivationActivityId = null, int? AssignedEngineerId = null, string? ReferralType = null, int? DeviceIncidentId = null);
public sealed record CropSupportAlert(int Id, int? SensorChannelId, string Type, string Severity, string Status,
    string Message, decimal? TriggeredValue, DateTimeOffset OpenedAt);
public sealed record CropConditionDetail(int Id, int FarmId, int? ZoneId, string Crop, string Description,
    DateTimeOffset ReportedAt, string Status, int ReporterId, int? EngineerId, int Revision,
    int[] SensorChannelIds, EvidenceImageData[] Images, CropConsultationDetail[] Updates,
    int? CropSeasonId = null, DateTimeOffset? ObservedAt = null, SupportingCropSeason? LinkedSeason = null,
    bool CanReadEvidence = false, string[]? AllowedActions = null, int HistoryCount = 0);

public sealed class CropSupportService(ICropSupportRepository repository, ICurrentUser current, TimeProvider clock,
    IValidator<PageQuery> pages, IValidator<CreateCropConditionRequest> creates, IValidator<CropConsultationRequest> updates)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<SupportingCropSeason>> SeasonsAsync(int id, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.SeasonsAsync(id, Actor, query, ct);
    }
    public async Task<PagedResult<CropSensorReading>> ReadingsAsync(int id, CropEvidenceQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        CropObservationTime.Validate(query);
        return await repository.ReadingsAsync(id, Actor, query, ct);
    }
    public async Task<PagedResult<ActivitySummary>> ActivitiesAsync(int id, CropEvidenceQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct); CropObservationTime.Validate(query);
        return await repository.ActivitiesAsync(id, Actor, query, ct);
    }
    public Task<EvidenceImageData[]> ActivityImagesAsync(int id, int activityId, CancellationToken ct)
        => repository.ActivityImagesAsync(id, activityId, Actor, ct);
    public async Task<PagedResult<CropSupportAlert>> AlertsAsync(int id, CropEvidenceQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct); CropObservationTime.Validate(query);
        return await repository.AlertsAsync(id, Actor, query, ct);
    }
    public async Task<PagedResult<CropConsultationDetail>> HistoryAsync(int id, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.HistoryAsync(id, Actor, query, ct);
    }
    public Task<CropConditionDetail> AssignAsync(int id, AssignCropReportRequest request, CancellationToken ct)
        => UpdateAsync(id, CropConsultationAction.ASSIGNED, new(request.ExpectedRevision, "Administrator assigned the report.", EngineerId: request.EngineerId), ct);
    public Task<CropConditionDetail> FollowUpAsync(int id, CropFollowUpRequest request, CancellationToken ct)
        => UpdateAsync(id, CropConsultationAction.FOLLOW_UP, new(request.ExpectedRevision, request.Content,
            Images: request.Images, FollowUpType: request.Type, CultivationActivityId: request.CultivationActivityId), ct);
    public Task<CropConditionDetail> ReferAsync(int id, CropReferralRequest request, CancellationToken ct)
        => UpdateAsync(id, CropConsultationAction.REFERRED, new(request.ExpectedRevision, request.Content,
            Images: request.Images, ReferralType: request.Type, DeviceIncidentId: request.DeviceIncidentId), ct);
    public async Task<CropConditionDetail> ClaimAsync(int id, CancellationToken ct)
    {
        await repository.ApplyAsync(id, Actor, CropConsultationAction.CLAIMED, null, [], clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
    public async Task<CropConditionDetail> UpdateAsync(int id, CropConsultationAction action, CropConsultationRequest request, CancellationToken ct)
    {
        await updates.ValidateAndThrowAsync(request, ct);
        if (request.RecommendSiteSurvey && action != CropConsultationAction.RECOMMENDED)
            throw new BusinessRuleException("On-site survey recommendations belong to the recommendation action.");
        if (action == CropConsultationAction.ASSIGNED && request.EngineerId is not > 0
            || action != CropConsultationAction.ASSIGNED && request.EngineerId.HasValue
            || action == CropConsultationAction.FOLLOW_UP && request.FollowUpType is not ("ACKNOWLEDGED" or "AGREED" or "QUESTION" or "ACTION_TAKEN" or "IMPROVED" or "NOT_IMPROVED")
            || action != CropConsultationAction.FOLLOW_UP && (request.FollowUpType != null || request.CultivationActivityId != null)
            || request.CultivationActivityId <= 0 || request.DeviceIncidentId <= 0
            || action == CropConsultationAction.REFERRED && request.ReferralType is not ("DEVICE_INCIDENT" or "UAV_VIDEO")
            || action != CropConsultationAction.REFERRED && (request.ReferralType != null || request.DeviceIncidentId != null)
            || request.ReferralType == "UAV_VIDEO" && request.DeviceIncidentId != null)
            throw new ValidationException("Invalid fields for this crop support action.");
        var images = EvidenceImages.Decode(request.Images);
        await repository.ApplyAsync(id, Actor, action, request, images, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
    public async Task<PagedResult<CropConditionSummary>> ListAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(Actor, query, ct);
    }
    public Task<CropConditionDetail> GetAsync(int id, CancellationToken ct) => repository.GetAsync(id, Actor, ct);
    public async Task<CropConditionDetail> CreateAsync(CreateCropConditionRequest request, CancellationToken ct)
    {
        await creates.ValidateAndThrowAsync(request, ct);
        var observed = CropObservationTime.Parse(request.ObservedAt);
        if (observed < new DateTimeOffset(1900, 1, 8, 0, 0, 0, TimeSpan.Zero) || observed > clock.GetUtcNow().AddMinutes(5))
            throw new ValidationException("Observation time must be between 1900-01-08 and now (5 minute clock tolerance).");
        var images = EvidenceImages.Decode(request.Images);
        var id = await repository.CreateAsync(Actor, request, images, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
}
