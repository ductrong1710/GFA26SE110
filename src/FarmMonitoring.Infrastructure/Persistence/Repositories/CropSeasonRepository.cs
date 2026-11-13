using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.CropSeasons;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class CropSeasonRepository(AppDbContext db) : ICropSeasonRepository
{
    public async Task<SeasonComparison[]> CompareAsync(int actor, int[] ids, CancellationToken ct)
    {
        if (!await db.UserRoles.AnyAsync(x => x.UserId == actor && x.User.IsActive && x.Role.Name == RoleNames.FarmOwner, ct))
            throw new AccessDeniedException("Farm Owner access required.");
        var farms = FarmAccessQuery.AssignedFarmIds(db, actor);
        var seasons = db.CropSeasons.AsNoTracking().Where(x => ids.Contains(x.Id) && x.OwnerId == actor && farms.Contains(x.FarmId));
        var selected = await seasons.Select(x => new { x.Id, x.Name, x.Crop, x.AreaHectares, x.StartDate, x.EndDate,
            x.YieldKg, x.TotalCostVnd, x.RevenueVnd }).ToDictionaryAsync(x => x.Id, ct);
        if (selected.Count != ids.Length)
        {
            if (await db.CropSeasons.CountAsync(x => ids.Contains(x.Id), ct) != ids.Length)
                throw new NotFoundException("A selected season was not found.");
            throw AccessDeniedException.OutsideScope();
        }
        var counts = await db.CultivationActivities.Where(x => seasons.Any(s => s.Id == x.SeasonId))
            .GroupBy(x => new { x.SeasonId, x.Type })
            .Select(g => new { g.Key.SeasonId, g.Key.Type, Count = g.Count() }).ToArrayAsync(ct);
        var activities = counts.OrderBy(x => x.Type).ToLookup(x => x.SeasonId, x => new ActivityTypeCount(x.Type.ToString(), x.Count));
        return ids.Select(id =>
        {
            var s = selected[id];
            return new SeasonComparison(id, s.Name, s.Crop, s.AreaHectares, s.StartDate, s.EndDate,
                s.YieldKg, s.TotalCostVnd, s.RevenueVnd, activities[id].ToArray());
        }).ToArray();
    }
    private async Task<IQueryable<CropSeason>> VisibleAsync(int actor, CancellationToken ct)
    {
        var roles = await db.UserRoles.Where(x => x.UserId == actor && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
        var admin = roles.Contains(RoleNames.FarmAdministrator);
        if (!admin && !roles.Contains(RoleNames.FarmOwner)) throw new AccessDeniedException("Crop season access denied.");
        var farms = FarmAccessQuery.AssignedFarmIds(db, actor);
        return db.CropSeasons.AsNoTracking().Where(x => admin || x.OwnerId == actor && farms.Contains(x.FarmId));
    }
    private static IQueryable<CropSeasonDetail> Details(IQueryable<CropSeason> rows) => rows
            .Select(x => new CropSeasonDetail(x.Id, x.OwnerId, x.FarmId, x.ZoneId, x.Name, x.Crop,
                x.AreaHectares, x.StartDate, x.ExpectedEndDate, x.CropAgeDays, x.Notes, x.Status.ToString(),
                x.Revision, x.EndDate, x.YieldKg, x.TotalCostVnd, x.RevenueVnd, x.CompletionNotes));

    public async Task<PagedResult<CropSeasonDetail>> ListAsync(int actor, CropSeasonQuery query, CancellationToken ct)
    {
        var rows = await VisibleAsync(actor, ct);
        int? zoneFarmId = null;
        if (query.ZoneId is int selectedZone)
        {
            zoneFarmId = await db.Zones.Where(x => x.Id == selectedZone).Select(x => (int?)x.FarmId).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Zone not found.");
            if (query.FarmId.HasValue && query.FarmId != zoneFarmId)
                throw new BusinessRuleException("The selected zone must belong to the requested farm.");
        }
        if ((query.FarmId ?? zoneFarmId) is int requestedFarm)
        {
            if (!await db.UserRoles.AnyAsync(x => x.UserId == actor && x.User.IsActive && x.Role.Name == RoleNames.FarmAdministrator, ct)
                && !await FarmAccessQuery.AssignedFarmIds(db, actor).ContainsAsync(requestedFarm, ct))
                throw AccessDeniedException.OutsideScope();
        }
        if (query.FarmId is int farmId) rows = rows.Where(x => x.FarmId == farmId);
        if (query.ZoneId is int zoneId) rows = rows.Where(x => x.ZoneId == zoneId);
        if (query.WholeFarmOnly) rows = rows.Where(x => x.ZoneId == null);
        var total = await rows.CountAsync(ct);
        var items = await Details(rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
    public async Task<CropSeasonDetail> GetAsync(int actor, int id, CancellationToken ct)
    {
        var rows = await VisibleAsync(actor, ct);
        var result = await Details(rows.Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        if (result is not null) return result;
        if (await db.CropSeasons.AnyAsync(x => x.Id == id, ct)) throw AccessDeniedException.OutsideScope();
        throw new NotFoundException("Crop season not found.");
    }
    private async Task LockOwnerAsync(int actor, CancellationToken ct)
    {
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actor} FOR UPDATE").ToArrayAsync(ct);
        if (!await db.UserRoles.AnyAsync(x => x.UserId == actor && x.User.IsActive && x.Role.Name == RoleNames.FarmOwner, ct))
            throw new AccessDeniedException("Farm Owner access required.");
    }
    private async Task<CropSeason> LockSeasonAsync(int id, CancellationToken ct) =>
        await db.CropSeasons.FromSqlInterpolated($"SELECT * FROM crop_seasons WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Crop season not found.");

    private async Task RequireFarmAccessAsync(int actor, CropSeason row, CancellationToken ct)
    {
        if (row.OwnerId != actor || !await db.UserFarms.FromSqlInterpolated(
            $"SELECT * FROM user_farms WHERE user_id = {actor} AND farm_id = {row.FarmId} FOR UPDATE").AnyAsync(ct))
            throw AccessDeniedException.OutsideScope();
        if (!await db.Farms.AnyAsync(x => x.Id == row.FarmId && x.IsActive, ct))
            throw new BusinessRuleException("An active farm is required.");
    }
    private static void Advance(CropSeason row, int revision, DateTimeOffset now)
    {
        if (row.Status != CropSeasonStatus.ACTIVE || row.Revision != revision)
            throw new ConflictException("The season changed or is complete. Reload the season.");
        row.Revision++;
        row.UpdatedAt = now;
    }
    private async Task<CropSeason> LockActiveSeasonAsync(int actor, int id, int revision, DateTimeOffset now, CancellationToken ct)
    {
        await LockOwnerAsync(actor, ct);
        var row = await LockSeasonAsync(id, ct);
        await RequireFarmAccessAsync(actor, row, ct);
        Advance(row, revision, now);
        return row;
    }
    public async Task RecordActivityAsync(int actor, int id, CultivationActivityRequest request, List<EvidenceImage> images, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var season = await LockActiveSeasonAsync(actor, id, request.ExpectedRevision, now, ct);
        db.CultivationActivities.Add(new CultivationActivity
        {
            SeasonId = season.Id, UserId = actor, Date = request.Date, Type = Enum.Parse<CultivationActivityType>(request.Type),
            Description = request.Description.Trim(), CostVnd = request.CostVnd, Notes = request.Notes?.Trim(), CreatedAt = now, Images = images
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    private async Task<IQueryable<CultivationActivity>> VisibleActivitiesAsync(int actor, int id, CancellationToken ct)
    {
        var seasons = (await VisibleAsync(actor, ct)).Where(x => x.Id == id);
        if (!await seasons.AnyAsync(ct))
        {
            if (await db.CropSeasons.AnyAsync(x => x.Id == id, ct)) throw AccessDeniedException.OutsideScope();
            throw new NotFoundException("Crop season not found.");
        }
        return db.CultivationActivities.AsNoTracking().Where(x => seasons.Any(s => s.Id == x.SeasonId));
    }
    public async Task<PagedResult<ActivitySummary>> ActivitiesAsync(int actor, int id, PageQuery query, CancellationToken ct)
    {
        var rows = await VisibleActivitiesAsync(actor, id, ct);
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ActivitySummary(x.Id, x.UserId, x.Date, x.Type.ToString(), x.Description,
                x.CostVnd, x.Notes, x.CreatedAt, x.Images.Count)).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
    public async Task<EvidenceImageData[]> ImagesAsync(int actor, int id, int activityId, CancellationToken ct)
    {
        var rows = await VisibleActivitiesAsync(actor, id, ct);
        var activity = await rows.Include(x => x.Images).SingleOrDefaultAsync(x => x.Id == activityId, ct)
            ?? throw new NotFoundException("Activity not found in this season.");
        return EvidenceImages.Encode(activity.Images);
    }
    public async Task CompleteAsync(int actor, int id, CompleteCropSeasonRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await LockActiveSeasonAsync(actor, id, request.ExpectedRevision, now, ct);
        if (request.EndDate < row.StartDate)
            throw new BusinessRuleException("End date must not precede start date.");
        row.Status = CropSeasonStatus.COMPLETED;
        row.CompletedAt = now;
        row.EndDate = request.EndDate;
        row.YieldKg = request.YieldKg;
        row.TotalCostVnd = request.TotalCostVnd;
        row.RevenueVnd = request.RevenueVnd;
        row.CompletionNotes = request.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task<int> SaveAsync(int actor, int? id, CropSeasonRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockOwnerAsync(actor, ct);
        var row = id.HasValue ? await LockSeasonAsync(id.Value, ct)
            : new CropSeason { FarmId = request.FarmId, ZoneId = request.ZoneId, OwnerId = actor, CreatedAt = now };
        await RequireFarmAccessAsync(actor, row, ct);
        if (row.FarmId != request.FarmId || row.ZoneId != request.ZoneId)
            throw new BusinessRuleException("The season farm and zone cannot change.");
        if (row.ZoneId is int zone && !await db.Zones.AnyAsync(x => x.Id == zone && x.FarmId == row.FarmId, ct))
            throw new BusinessRuleException("Choose a zone in this farm.");
        if (id.HasValue)
            Advance(row, request.ExpectedRevision, now);
        else db.CropSeasons.Add(row);
        row.Name = request.Name.Trim();
        row.Crop = request.Crop.Trim();
        row.AreaHectares = request.AreaHectares;
        row.StartDate = request.StartDate;
        row.ExpectedEndDate = request.ExpectedEndDate;
        row.CropAgeDays = request.CropAgeDays;
        row.Notes = request.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return row.Id;
    }
}
