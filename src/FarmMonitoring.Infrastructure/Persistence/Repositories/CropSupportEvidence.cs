using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.CropSeasons;
using FarmMonitoring.Application.Features.CropSupport;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed partial class CropSupportRepository
{
    private async Task<CropConditionReport> EvidenceReportAsync(int id, int actorId, CancellationToken ct)
    {
        var row = await (await ScopeAsync(actorId, ct)).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            if (await db.CropConditionReports.AnyAsync(x => x.Id == id, ct)) throw AccessDeniedException.OutsideScope();
            throw new NotFoundException("Crop condition report not found.");
        }
        var roles = await db.UserRoles.Where(x => x.UserId == actorId && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
        if (!(roles.Contains(RoleNames.FarmAdministrator) || roles.Contains(RoleNames.FarmEngineer) && row.EngineerId == actorId ||
              roles.Contains(RoleNames.FarmOwner) && row.ReporterId == actorId))
            throw new AccessDeniedException("Evidence is available only after assignment to this engineer.");
        return row;
    }

    private IQueryable<CropSeason> LinkedSeasons(CropConditionReport report) => db.CropSeasons.AsNoTracking().Where(s =>
        s.Id == report.CropSeasonId && s.OwnerId == report.ReporterId && s.FarmId == report.FarmId && s.ZoneId == report.ZoneId);
    private IQueryable<CultivationActivity> LinkedActivities(CropConditionReport report)
    {
        var seasons = LinkedSeasons(report).Select(s => s.Id);
        return db.CultivationActivities.AsNoTracking().Where(a => seasons.Contains(a.SeasonId));
    }

    public async Task<PagedResult<ActivitySummary>> ActivitiesAsync(int id, int actorId, CropEvidenceQuery query, CancellationToken ct)
    {
        var report = await EvidenceReportAsync(id, actorId, ct);
        var rows = LinkedActivities(report);
        var (from, to) = CropObservationTime.Window(query, report.ObservedAt);
        if (from.HasValue)
        {
            var first = DateOnly.FromDateTime(from.Value.UtcDateTime); var last = DateOnly.FromDateTime(to!.Value.UtcDateTime);
            rows = rows.Where(a => a.Date >= first && a.Date <= last);
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(a => new ActivitySummary(a.Id, a.UserId, a.Date, a.Type.ToString(), a.Description,
                null, a.Notes, a.CreatedAt, a.Images.Count)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<EvidenceImageData[]> ActivityImagesAsync(int id, int activityId, int actorId, CancellationToken ct)
    {
        var report = await EvidenceReportAsync(id, actorId, ct);
        var row = await LinkedActivities(report).Include(a => a.Images).SingleOrDefaultAsync(a => a.Id == activityId, ct)
            ?? throw new NotFoundException("Activity not found in this report's linked season.");
        return EvidenceImages.Encode(row.Images);
    }

    public async Task<PagedResult<CropSupportAlert>> AlertsAsync(int id, int actorId, CropEvidenceQuery query, CancellationToken ct)
    {
        var report = await EvidenceReportAsync(id, actorId, ct);
        var channels = db.SensorChannels.Where(c => c.SensorNode.Zone.FarmId == report.FarmId &&
            (report.ZoneId == null || c.SensorNode.ZoneId == report.ZoneId) &&
            db.Set<CropConditionSensor>().Any(s => s.ReportId == id && s.SensorChannelId == c.Id));
        if (query.SensorChannelId is int channel) channels = channels.Where(c => c.Id == channel);
        var rows = db.Alerts.AsNoTracking().Where(a => a.SensorChannelId != null && channels.Any(c => c.Id == a.SensorChannelId)
            || a.SensorChannelId == null && a.SensorNodeId != null && channels.Any(c => c.SensorNodeId == a.SensorNodeId));
        var (from, to) = CropObservationTime.Window(query, report.ObservedAt);
        if (from.HasValue) rows = rows.Where(a => a.OpenedAt >= from && a.OpenedAt <= to);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(a => a.OpenedAt).ThenByDescending(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(a => new CropSupportAlert(a.Id, a.SensorChannelId, a.AlertType.ToString(), a.Severity.ToString(),
                a.Status.ToString(), a.Message, a.TriggeredValue, a.OpenedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }

    private static CropConsultationDetail UpdateDetail(CropConsultationUpdate x) => new(x.Id, x.UserId,
        x.Action.ToString(), x.Content, x.RecommendSiteSurvey, x.CreatedAt, EvidenceImages.Encode(x.Images),
        x.FollowUpType, x.CultivationActivityId, x.AssignedEngineerId, x.ReferralType, x.DeviceIncidentId);

    public async Task<PagedResult<CropConsultationDetail>> HistoryAsync(int id, int actorId, PageQuery query, CancellationToken ct)
    {
        await EvidenceReportAsync(id, actorId, ct);
        var rows = db.CropConsultationUpdates.AsNoTracking().Where(x => x.ReportId == id);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Include(x => x.Images).AsSplitQuery().ToArrayAsync(ct);
        return new(items.Select(UpdateDetail).ToArray(), count, query.Page, query.PageSize);
    }

    private async Task<string[]> AllowedActionsAsync(CropConditionReport row, int actorId, string[] roles, CancellationToken ct)
    {
        var actions = new List<string>();
        if (row.Status == CropConditionStatus.NEW && row.EngineerId is null)
        {
            if (roles.Contains(RoleNames.FarmAdministrator)) actions.Add("assign");
            if (roles.Contains(RoleNames.FarmEngineer)) actions.Add("claim");
        }
        if (row.EngineerId == actorId && roles.Contains(RoleNames.FarmEngineer))
        {
            if (row.Status == CropConditionStatus.IN_REVIEW) actions.AddRange(["request-information", "recommend", "resolve"]);
            if (row.Status is CropConditionStatus.IN_REVIEW or CropConditionStatus.AWAITING_INFORMATION) actions.Add("refer");
            if (row.Status == CropConditionStatus.RESOLVED) actions.Add("reopen");
        }
        if (row.ReporterId == actorId && roles.Contains(RoleNames.FarmOwner) &&
            await FarmAccessQuery.AssignedFarmIds(db, actorId).ContainsAsync(row.FarmId, ct))
        {
            if (row.Status == CropConditionStatus.AWAITING_INFORMATION) actions.Add("reply");
            if (row.Status == CropConditionStatus.RESOLVED || row.Status != CropConditionStatus.NEW &&
                await db.CropConsultationUpdates.AnyAsync(x => x.ReportId == row.Id && x.Action == CropConsultationAction.RECOMMENDED, ct))
                actions.Add("follow-ups");
            if (row.Status is CropConditionStatus.IN_REVIEW or CropConditionStatus.AWAITING_INFORMATION &&
                await db.CropConsultationUpdates.AnyAsync(x => x.ReportId == row.Id && x.ReferralType == "DEVICE_INCIDENT", ct))
                actions.Add("link-incident");
        }
        return actions.ToArray();
    }
}
