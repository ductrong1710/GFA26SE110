using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.CropSupport;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed partial class CropSupportRepository(AppDbContext db, IDeviceIncidentRepository incidents) : ICropSupportRepository
{
    public async Task<PagedResult<SupportingCropSeason>> SeasonsAsync(int id, int actorId, PageQuery query, CancellationToken ct)
    {
        var reports = db.CropConditionReports.Where(r => r.Id == id && r.EngineerId == actorId &&
            db.UserRoles.Any(role => role.UserId == actorId && role.User.IsActive && role.Role.Name == RoleNames.FarmEngineer));
        if (!await reports.AnyAsync(ct))
        {
            if (!await db.CropConditionReports.AnyAsync(r => r.Id == id, ct)) throw new NotFoundException("Crop condition report not found.");
            throw new AccessDeniedException("Only the assigned Engineer can view supporting seasons.");
        }
        var seasons = db.CropSeasons.Where(s => reports.Any(r => r.ReporterId == s.OwnerId && r.FarmId == s.FarmId &&
            (r.ZoneId == null || r.ZoneId == s.ZoneId)));
        var count = await seasons.CountAsync(ct);
        var items = await seasons.OrderByDescending(s => s.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(s => new SupportingCropSeason(s.Id, s.Name, s.Crop, s.StartDate, s.EndDate, s.Status.ToString())).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<PagedResult<CropSensorReading>> ReadingsAsync(int id, int actorId, CropEvidenceQuery query, CancellationToken ct)
    {
        var report = await EvidenceReportAsync(id, actorId, ct);
        var (from, to) = CropObservationTime.Window(query, report.ObservedAt);
        var reports = (await ScopeAsync(actorId, ct)).Where(x => x.Id == id);
        if (!await reports.AnyAsync(ct))
        {
            if (await db.CropConditionReports.AnyAsync(x => x.Id == id, ct)) throw AccessDeniedException.OutsideScope();
            throw new NotFoundException("Crop condition report not found.");
        }
        var rows = db.SensorReadings.AsNoTracking().Where(reading => reports.Any(report =>
            report.Sensors.Any(sensor => sensor.SensorChannelId == reading.SensorChannelId) &&
            reading.SensorChannel.SensorNode.Zone.FarmId == report.FarmId &&
            (report.ZoneId == null || reading.SensorChannel.SensorNode.ZoneId == report.ZoneId)));
        if (query.SensorChannelId is int channel) rows = rows.Where(x => x.SensorChannelId == channel);
        if (from.HasValue) rows = rows.Where(x => x.MeasuredAt >= from && x.MeasuredAt <= to);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.MeasuredAt).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CropSensorReading(x.Id, x.SensorChannelId, x.SensorChannel.ChannelCode, x.SensorChannel.SensorType.Unit,
                x.Value, x.MeasuredAt, x.IsValid, x.QualityStatus)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task ApplyAsync(int id, int actorId, CropConsultationAction action, CropConsultationRequest? request,
        List<EvidenceImage> images, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var targetUser = action == CropConsultationAction.ASSIGNED ? request?.EngineerId ?? actorId : actorId;
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} OR id = {targetUser} ORDER BY id FOR UPDATE").ToArrayAsync(ct);
        var reply = action is CropConsultationAction.OWNER_REPLIED or CropConsultationAction.FOLLOW_UP ||
            action == CropConsultationAction.REFERRED && request?.DeviceIncidentId != null;
        var assign = action == CropConsultationAction.ASSIGNED;
        var role = assign ? RoleNames.FarmAdministrator : reply ? RoleNames.FarmOwner : RoleNames.FarmEngineer;
        if (!await db.UserRoles.AnyAsync(x => x.UserId == actorId && x.User.IsActive && x.Role.Name == role, ct))
            throw new AccessDeniedException("This crop support action is not permitted for your role.");
        var row = await db.CropConditionReports.FromSqlInterpolated($"SELECT * FROM crop_condition_reports WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Crop condition report not found.");
        if (reply)
        {
            if (row.ReporterId != actorId || !await db.UserFarms.FromSqlInterpolated(
                $"SELECT * FROM user_farms WHERE user_id = {actorId} AND farm_id = {row.FarmId} FOR UPDATE").AnyAsync(ct))
                throw new AccessDeniedException("Only the reporter with current farm access can reply.");
        }
        else if (!assign && action != CropConsultationAction.CLAIMED && row.EngineerId != actorId)
            throw new AccessDeniedException("Only the assigned Engineer can process this report.");
        if (action != CropConsultationAction.CLAIMED && (request is null || request.ExpectedRevision != row.Revision))
            throw new ConflictException("The report changed. Reload its latest revision.");
        switch (action)
        {
            case CropConsultationAction.ASSIGNED when row.Status == CropConditionStatus.NEW && row.EngineerId is null:
                if (!await db.UserRoles.AnyAsync(x => x.UserId == request!.EngineerId && x.User.IsActive && x.Role.Name == RoleNames.FarmEngineer, ct))
                    throw new BusinessRuleException("Choose an active FarmEngineer.");
                row.EngineerId = request!.EngineerId; row.Status = CropConditionStatus.IN_REVIEW;
                break;
            case CropConsultationAction.CLAIMED when row.Status == CropConditionStatus.NEW && row.EngineerId is null:
                row.EngineerId = actorId; row.Status = CropConditionStatus.IN_REVIEW;
                break;
            case CropConsultationAction.INFORMATION_REQUESTED when row.Status == CropConditionStatus.IN_REVIEW:
                row.Status = CropConditionStatus.AWAITING_INFORMATION;
                break;
            case CropConsultationAction.OWNER_REPLIED when row.Status == CropConditionStatus.AWAITING_INFORMATION:
                row.Status = CropConditionStatus.IN_REVIEW;
                break;
            case CropConsultationAction.RECOMMENDED when row.Status == CropConditionStatus.IN_REVIEW:
                break;
            case CropConsultationAction.RESOLVED when row.Status == CropConditionStatus.IN_REVIEW:
                row.Status = CropConditionStatus.RESOLVED;
                break;
            case CropConsultationAction.FOLLOW_UP when row.Status != CropConditionStatus.NEW:
                if (row.Status == CropConditionStatus.RESOLVED)
                {
                    if (request!.FollowUpType != "NOT_IMPROVED") throw new ConflictException("A closed report accepts a non-improvement review request; only its engineer can reopen it.");
                    action = CropConsultationAction.REVIEW_REQUESTED;
                }
                else if (!await db.CropConsultationUpdates.AnyAsync(x => x.ReportId == id && x.Action == CropConsultationAction.RECOMMENDED, ct))
                    throw new ConflictException("Follow-up is available after a recommendation.");
                if (request!.CultivationActivityId is int activityId && !await LinkedActivities(row)
                    .AnyAsync(x => x.Id == activityId, ct))
                    throw new BusinessRuleException("The activity must belong to the linked season owned by this reporter.");
                break;
            case CropConsultationAction.REOPENED when row.Status == CropConditionStatus.RESOLVED:
                row.Status = CropConditionStatus.IN_REVIEW;
                break;
            case CropConsultationAction.REFERRED when row.Status is CropConditionStatus.IN_REVIEW or CropConditionStatus.AWAITING_INFORMATION:
                if (request!.DeviceIncidentId is int incidentId)
                {
                    if (!await db.CropConsultationUpdates.AnyAsync(x => x.ReportId == id && x.ReferralType == "DEVICE_INCIDENT", ct))
                        throw new ConflictException("An engineer must request a device referral first.");
                    var incident = await incidents.GetAsync(incidentId, actorId, ct);
                    if (incident.FarmId != row.FarmId) throw new BusinessRuleException("Incident must belong to the report farm.");
                    var device = await db.RentalDeviceAssignments.Where(x => x.Id == incident.RentalDeviceAssignmentId)
                        .Select(x => new { x.SensorNodeId }).SingleOrDefaultAsync(ct);
                    if (device?.SensorNodeId is int nodeId && !await db.SensorNodes.AnyAsync(x => x.Id == nodeId &&
                        x.Zone.FarmId == row.FarmId && (row.ZoneId == null || x.ZoneId == row.ZoneId) &&
                        (!db.Set<CropConditionSensor>().Any(s => s.ReportId == id) || db.SensorChannels.Any(c => c.SensorNodeId == nodeId &&
                            db.Set<CropConditionSensor>().Any(s => s.ReportId == id && s.SensorChannelId == c.Id))), ct))
                        throw new BusinessRuleException("Incident device must match the report zone and attached channels.");
                }
                break;
            default:
                throw new ConflictException("This action is not allowed in the current report state.");
        }
        if (row.Revision == int.MaxValue) throw new ConflictException("Report revision limit reached.");
        row.Revision++;
        db.CropConsultationUpdates.Add(new() { ReportId = id, UserId = actorId, Action = action,
            Content = request?.Content.Trim() ?? "Engineer accepted the report.", RecommendSiteSurvey = request?.RecommendSiteSurvey ?? false,
            FollowUpType = request?.FollowUpType, CultivationActivityId = request?.CultivationActivityId,
            AssignedEngineerId = assign ? row.EngineerId : action == CropConsultationAction.CLAIMED ? actorId : null,
            ReferralType = request?.ReferralType, DeviceIncidentId = request?.DeviceIncidentId,
            CreatedAt = now, Images = images });
        var recipient = reply ? row.EngineerId : row.ReporterId;
        if (recipient is int userId && (reply
            ? await db.UserRoles.AnyAsync(x => x.UserId == userId && x.Role.Name == RoleNames.FarmEngineer, ct)
            : await FarmAccessQuery.AssignedFarmIds(db, userId).ContainsAsync(row.FarmId, ct)))
            await WorkflowNotifications.AddForUserAsync(db, userId, "Crop support updated",
                $"Crop report #{id}: {action}. Open the report to review the update.", now, ct);
        if (assign && row.EngineerId is int assignedId)
            await WorkflowNotifications.AddForUserAsync(db, assignedId, "Crop support assigned",
                $"Crop report #{id} was assigned to you by user #{actorId}.", now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private async Task<IQueryable<CropConditionReport>> ScopeAsync(int actor, CancellationToken ct)
    {
        var roles = await db.UserRoles.Where(x => x.UserId == actor && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
        var admin = roles.Contains(RoleNames.FarmAdministrator);
        var owner = roles.Contains(RoleNames.FarmOwner);
        var engineer = roles.Contains(RoleNames.FarmEngineer);
        if (!admin && !owner && !engineer) throw new AccessDeniedException("Crop support access denied.");
        var farms = FarmAccessQuery.AssignedFarmIds(db, actor);
        return db.CropConditionReports.AsNoTracking().Where(x => admin ||
            owner && x.ReporterId == actor && farms.Contains(x.FarmId) ||
            engineer && (x.EngineerId == actor || x.EngineerId == null && x.Status == CropConditionStatus.NEW));
    }
    public async Task<PagedResult<CropConditionSummary>> ListAsync(int actorId, PageQuery query, CancellationToken ct)
    {
        var rows = await ScopeAsync(actorId, ct);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CropConditionSummary(x.Id, x.FarmId, x.ZoneId, x.Crop, x.Description, x.ReportedAt,
                x.Status.ToString(), x.ReporterId, x.EngineerId, x.Revision, x.CropSeasonId, x.ObservedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<CropConditionDetail> GetAsync(int id, int actorId, CancellationToken ct)
    {
        var rows = await ScopeAsync(actorId, ct);
        var row = await rows.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            if (await db.CropConditionReports.AnyAsync(x => x.Id == id, ct)) throw AccessDeniedException.OutsideScope();
            throw new NotFoundException("Crop condition report not found.");
        }
        var roles = await db.UserRoles.Where(x => x.UserId == actorId && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
        var evidence = roles.Contains(RoleNames.FarmAdministrator) || row.EngineerId == actorId && roles.Contains(RoleNames.FarmEngineer)
            || row.ReporterId == actorId && roles.Contains(RoleNames.FarmOwner);
        if (!evidence) return new(row.Id, row.FarmId, row.ZoneId, row.Crop, row.Description, row.ReportedAt,
            row.Status.ToString(), row.ReporterId, row.EngineerId, row.Revision, [], [], [], row.CropSeasonId, row.ObservedAt,
            AllowedActions: ["claim"]);
        row = await rows.Include(x => x.Images).Include(x => x.Sensors)
            .Include(x => x.Updates.OrderByDescending(u => u.Id).Take(20)).ThenInclude(x => x.Images)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            if (await db.CropConditionReports.AnyAsync(x => x.Id == id, ct)) throw AccessDeniedException.OutsideScope();
            throw new NotFoundException("Crop condition report not found.");
        }
        var linked = await LinkedSeasons(row).Select(s => new SupportingCropSeason(s.Id, s.Name, s.Crop, s.StartDate, s.EndDate, s.Status.ToString())).SingleOrDefaultAsync(ct);
        var historyCount = await db.CropConsultationUpdates.CountAsync(x => x.ReportId == id, ct);
        return new(row.Id, row.FarmId, row.ZoneId, row.Crop, row.Description, row.ReportedAt, row.Status.ToString(),
            row.ReporterId, row.EngineerId, row.Revision, row.Sensors.Select(x => x.SensorChannelId).Order().ToArray(),
            EvidenceImages.Encode(row.Images), row.Updates.OrderBy(x => x.Id).Select(UpdateDetail).ToArray(),
            row.CropSeasonId, row.ObservedAt, linked, true, await AllowedActionsAsync(row, actorId, roles, ct), historyCount);
    }
    public async Task<int> CreateAsync(int actorId, CreateCropConditionRequest request, List<EvidenceImage> images, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").ToArrayAsync(ct);
        if (!await db.UserRoles.AnyAsync(x => x.UserId == actorId && x.User.IsActive && x.Role.Name == RoleNames.FarmOwner, ct))
            throw new AccessDeniedException("Farm Owner access required.");
        if (!await FarmAccessQuery.AssignedFarmIds(db, actorId).ContainsAsync(request.FarmId, ct))
            throw AccessDeniedException.OutsideScope();
        if (!await db.UserFarms.FromSqlInterpolated($"SELECT * FROM user_farms WHERE user_id = {actorId} AND farm_id = {request.FarmId} FOR UPDATE").AnyAsync(ct))
            throw AccessDeniedException.OutsideScope();
        var crop = request.Crop.Trim();
        if (request.CropSeasonId is int seasonId)
        {
            var season = await db.CropSeasons.FromSqlInterpolated($"SELECT * FROM crop_seasons WHERE id = {seasonId} FOR UPDATE")
                .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Crop season not found.");
            if (season.OwnerId != actorId) throw AccessDeniedException.OutsideScope();
            if (season.FarmId != request.FarmId || season.ZoneId != request.ZoneId || season.Crop != crop)
                throw new BusinessRuleException("Farm, zone and crop must match the selected season.");
            crop = season.Crop;
        }
        if (!await db.Farms.AnyAsync(x => x.Id == request.FarmId && x.IsActive, ct))
            throw new BusinessRuleException("An active farm is required.");
        if (request.ZoneId is int zone && !await db.Zones.AnyAsync(x => x.Id == zone && x.FarmId == request.FarmId, ct))
            throw new BusinessRuleException("The selected zone must belong to this farm.");
        var sensorIds = request.SensorChannelIds ?? [];
        if (await db.SensorChannels.CountAsync(x => sensorIds.Contains(x.Id) && x.SensorNode.Zone.FarmId == request.FarmId &&
            (request.ZoneId == null || x.SensorNode.ZoneId == request.ZoneId), ct) != sensorIds.Length)
            throw new BusinessRuleException("Sensor channels must belong to the selected farm and zone.");
        var row = new CropConditionReport { FarmId = request.FarmId, ZoneId = request.ZoneId, ReporterId = actorId,
            Crop = crop, CropSeasonId = request.CropSeasonId, ObservedAt = CropObservationTime.Parse(request.ObservedAt),
            Description = request.Description.Trim(), ReportedAt = now, Images = images,
            Sensors = sensorIds.Select(id => new CropConditionSensor { SensorChannelId = id }).ToList(),
            Updates = [new CropConsultationUpdate { UserId = actorId, Action = CropConsultationAction.SUBMITTED,
                Content = request.Description.Trim(), CreatedAt = now }] };
        db.CropConditionReports.Add(row);
        await db.SaveChangesAsync(ct);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmEngineer, "Crop condition reported",
            $"Crop condition report #{row.Id} for farm #{row.FarmId} requires review.", now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return row.Id;
    }
}
