using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.CropSupport;

public sealed record CropConditionSummary(int Id, int FarmId, int? ZoneId, string Crop, string Description,
    DateTimeOffset ReportedAt, string Status, int ReporterId, int? EngineerId, int Revision);
public sealed record SupportingCropSeason(int Id, string Name, string Crop, DateOnly? StartDate, DateOnly? EndDate, string Status);
public sealed record CropSensorReading(long Id, int SensorChannelId, string ChannelCode, string? Unit,
    decimal Value, DateTimeOffset MeasuredAt, bool IsValid, string? QualityStatus);
public sealed record CropConsultationDetail(int Id, int UserId, string Action, string Content,
    bool RecommendSiteSurvey, DateTimeOffset CreatedAt, EvidenceImageData[] Images);
public sealed record CropConditionDetail(int Id, int FarmId, int? ZoneId, string Crop, string Description,
    DateTimeOffset ReportedAt, string Status, int ReporterId, int? EngineerId, int Revision,
    int[] SensorChannelIds, EvidenceImageData[] Images, CropConsultationDetail[] Updates);

public sealed class CropSupportService(ICropSupportRepository repository, ICurrentUser current, TimeProvider clock,
    IValidator<PageQuery> pages, IValidator<CreateCropConditionRequest> creates, IValidator<CropConsultationRequest> updates)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<SupportingCropSeason>> SeasonsAsync(int id, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.SeasonsAsync(id, Actor, query, ct);
    }
    public async Task<PagedResult<CropSensorReading>> ReadingsAsync(int id, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ReadingsAsync(id, Actor, query, ct);
    }
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
        var images = EvidenceImages.Decode(request.Images);
        var id = await repository.CreateAsync(Actor, request, images, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
}
