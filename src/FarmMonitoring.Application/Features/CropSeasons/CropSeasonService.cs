using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.CropSeasons;

public sealed record CropSeasonDetail(int Id, int OwnerId, int FarmId, int? ZoneId, string Name, string Crop,
    decimal? AreaHectares, DateOnly? StartDate, DateOnly? ExpectedEndDate, int? CropAgeDays, string? Notes,
    string Status, int Revision, DateOnly? EndDate, decimal? YieldKg, decimal? TotalCostVnd, decimal? RevenueVnd, string? CompletionNotes);

public sealed record ActivitySummary(int Id, int UserId, DateOnly Date, string Type, string Description,
    decimal? CostVnd, string? Notes, DateTimeOffset CreatedAt, int ImageCount);

public sealed record ActivityTypeCount(string Type, int Count);
public sealed record SeasonComparison(int Id, string Name, string Crop, decimal? AreaHectares, DateOnly? StartDate,
    DateOnly? EndDate, decimal? YieldKg, decimal? TotalCostVnd, decimal? RevenueVnd, ActivityTypeCount[] Activities)
{
    public int? DurationDays => EndDate?.DayNumber - StartDate?.DayNumber;
    public decimal? ProfitVnd => RevenueVnd - TotalCostVnd;
}

public interface ICropSeasonRepository
{
    Task<SeasonComparison[]> CompareAsync(int actor, int[] ids, CancellationToken ct);
    Task RecordActivityAsync(int actor, int id, CultivationActivityRequest request, List<EvidenceImage> images, DateTimeOffset now, CancellationToken ct);
    Task<PagedResult<ActivitySummary>> ActivitiesAsync(int actor, int id, PageQuery query, CancellationToken ct);
    Task<EvidenceImageData[]> ImagesAsync(int actor, int id, int activityId, CancellationToken ct);
    Task CompleteAsync(int actor, int id, CompleteCropSeasonRequest request, DateTimeOffset now, CancellationToken ct);
    Task<PagedResult<CropSeasonDetail>> ListAsync(int actor, CropSeasonQuery query, CancellationToken ct);
    Task<CropSeasonDetail> GetAsync(int actor, int id, CancellationToken ct);
    Task<int> SaveAsync(int actor, int? id, CropSeasonRequest request, DateTimeOffset now, CancellationToken ct);
}

public sealed class CropSeasonService(ICropSeasonRepository repository, ICurrentUser current, TimeProvider clock,
    IValidator<PageQuery> pages, IValidator<CropSeasonRequest> requests, IValidator<CompleteCropSeasonRequest> completions,
    IValidator<CultivationActivityRequest> activities)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public Task<SeasonComparison[]> CompareAsync(int[] ids, CancellationToken ct)
    {
        if (ids.Length is < 2 or > 50 || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            throw new ValidationException("Choose 2 to 50 distinct seasons.");
        return repository.CompareAsync(Actor, ids, ct);
    }
    public async Task<CropSeasonDetail> RecordActivityAsync(int id, CultivationActivityRequest request, CancellationToken ct)
    {
        await activities.ValidateAndThrowAsync(request, ct);
        await repository.RecordActivityAsync(Actor, id, request, EvidenceImages.Decode(request.Images), clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
    public async Task<PagedResult<ActivitySummary>> ActivitiesAsync(int id, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ActivitiesAsync(Actor, id, query, ct);
    }
    public Task<EvidenceImageData[]> ImagesAsync(int id, int activityId, CancellationToken ct) =>
        repository.ImagesAsync(Actor, id, activityId, ct);
    public async Task<CropSeasonDetail> CompleteAsync(int id, CompleteCropSeasonRequest request, CancellationToken ct)
    {
        await completions.ValidateAndThrowAsync(request, ct);
        await repository.CompleteAsync(Actor, id, request, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
    public async Task<PagedResult<CropSeasonDetail>> ListAsync(CropSeasonQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        if (query.FarmId <= 0 || query.ZoneId <= 0 || query.WholeFarmOnly && query.ZoneId.HasValue)
            throw new ValidationException("Invalid season scope filters.");
        return await repository.ListAsync(Actor, query, ct);
    }
    public Task<CropSeasonDetail> GetAsync(int id, CancellationToken ct) => repository.GetAsync(Actor, id, ct);
    public async Task<CropSeasonDetail> SaveAsync(int? id, CropSeasonRequest request, CancellationToken ct)
    {
        await requests.ValidateAndThrowAsync(request, ct);
        if (id is null && request.ExpectedRevision != 0)
            throw new ConflictException("A new season must start at revision zero.");
        var savedId = await repository.SaveAsync(Actor, id, request, clock.GetUtcNow(), ct);
        return await GetAsync(savedId, ct);
    }
}
