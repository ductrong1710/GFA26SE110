using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Installations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed class InstallationReportRepository(AppDbContext db) : IInstallationReportRepository
{
    public async Task<PagedResult<ReportSummary>> ListAsync(int jobId, PageQuery query, CancellationToken ct)
    {
        var rows = db.InstallationReports.AsNoTracking().Where(x => x.JobId == jobId);
        var count = await rows.CountAsync(ct);
        return new(await rows.OrderByDescending(x => x.Revision).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ReportSummary(x.Id, x.Revision, x.TechnicianId, x.CreatedAt, x.Approved)).ToArrayAsync(ct), count, query.Page, query.PageSize);
    }
    public Task<InstallationReport?> GetAsync(int jobId, int id, CancellationToken ct) => db.InstallationReports.AsNoTracking().Include(x => x.Devices).Include(x => x.Images).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id && x.JobId == jobId, ct);
    public async Task<InstallationReport> SubmitAsync(int jobId, int actorId, InstallationReportRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var roles = await LockActorAsync(actorId, ct);
        var job = await LockJobAsync(jobId, ct);
        if (!roles.Contains(RoleNames.Technician) || job.TechnicianId != actorId) throw new AccessDeniedException("Assigned Technician access required.");
        CheckRevision(job, request.ExpectedRevision);
        if (job.Status is not (InstallationJobStatus.IN_PROGRESS or InstallationJobStatus.REWORK)) throw new ConflictException("Start work or resolve requested rework before submitting a report.");
        var installation = await db.InstallationRequests.Include(x => x.Proposal).ThenInclude(x => x.Lines).SingleAsync(x => x.Id == job.InstallationRequestId, ct);
        var lines = installation.Proposal.Lines;
        var allocated = await db.InstallationDeviceAllocations.Where(x => x.JobId == jobId && x.ReleasedAt == null).ToArrayAsync(ct);
        if (allocated.Length != request.Devices.Length || request.Devices.Any(d => !allocated.Any(a => a.ProposalLineId == d.ProposalLineId && a.SensorNodeId == d.SensorNodeId && a.UavId == d.UavId && a.GatewayId == d.GatewayId)))
            throw new BusinessRuleException("Report exactly the devices allocated to this job by the administrator.");
        if (request.Devices.Any(d => lines.All(l => l.Id != d.ProposalLineId)) || lines.Any(l => request.Devices.Count(d => d.ProposalLineId == l.Id) != l.Quantity))
            throw new BusinessRuleException("Record every proposed device quantity using this job's proposal lines.");
        if (request.Devices.Select(d => (d.SensorNodeId, d.UavId, d.GatewayId)).Distinct().Count() != request.Devices.Length)
            throw new BusinessRuleException("A physical device can appear only once in a report.");
        var sensors = request.Devices.Where(x => x.SensorNodeId.HasValue).Select(x => x.SensorNodeId!.Value).ToArray();
        var uavs = request.Devices.Where(x => x.UavId.HasValue).Select(x => x.UavId!.Value).ToArray();
        var gateways = request.Devices.Where(x => x.GatewayId.HasValue).Select(x => x.GatewayId!.Value).ToArray();
        if (await db.SensorNodes.CountAsync(x => sensors.Contains(x.Id) && x.IsActive && x.Zone.FarmId == installation.FarmId, ct) != sensors.Length ||
            await db.Uavs.CountAsync(x => uavs.Contains(x.Id) && x.IsActive, ct) != uavs.Length || await db.Gateways.CountAsync(x => gateways.Contains(x.Id) && x.IsActive, ct) != gateways.Length)
            throw new BusinessRuleException("Devices must be registered and active; sensor nodes must belong to this farm.");
        var report = new InstallationReport { Images = EvidenceImages.Decode(request.Images), JobId = jobId, TechnicianId = actorId, CreatedAt = now, Notes = request.Notes.Trim(), Issues = request.Issues?.Trim(),
            Revision = (await db.InstallationReports.Where(x => x.JobId == jobId).MaxAsync(x => (int?)x.Revision, ct) ?? 0) + 1,
            Devices = request.Devices.Select(d => new InstalledDeviceCheck { ProposalLineId = d.ProposalLineId, SensorNodeId = d.SensorNodeId, UavId = d.UavId, GatewayId = d.GatewayId,
                InstallationLocation = d.InstallationLocation.Trim(), AccessoriesChecked = d.AccessoriesChecked!.Value, ConnectivityPassed = d.ConnectivityPassed!.Value,
                OperationPassed = d.OperationPassed!.Value, TestResults = d.TestResults.Trim() }).ToList() };
        db.InstallationReports.Add(report); job.Status = InstallationJobStatus.REPORT_SUBMITTED; job.Revision++;
        History(job, InstallationJobAction.REPORT_SUBMITTED, actorId, $"Installation report revision {report.Revision} submitted.", now);
        await WorkflowNotifications.AddForUserAsync(db, installation.OwnerId, "Installation handover confirmation required", $"Review the latest report for installation job #{jobId}.", now, ct);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Installation report submitted", $"Installation job #{jobId} has a report awaiting handover confirmation and review.", now, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return report;
    }
    public async Task DecideAsync(int jobId, int actorId, string action, ReportDecisionRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var roles = await LockActorAsync(actorId, ct);
        var job = await LockJobAsync(jobId, ct);
        var installation = await db.InstallationRequests.SingleAsync(x => x.Id == job.InstallationRequestId, ct);
        if (action == "confirm-handover")
        {
            if (!roles.Contains(RoleNames.FarmOwner) || installation.OwnerId != actorId || !await FarmAccessQuery.AssignedFarmIds(db, actorId).ContainsAsync(installation.FarmId, ct))
                throw new AccessDeniedException("Linked Farm Owner access required.");
        }
        else if (!roles.Contains(RoleNames.FarmAdministrator)) throw new AccessDeniedException("Administrator access required.");
        CheckRevision(job, request.ExpectedRevision);
        var report = await db.InstallationReports.Include(x => x.Devices).Where(x => x.JobId == jobId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        if (report is null || report.Id != request.ReportId) throw new ConflictException("Review the latest report version.");
        if (action == "confirm-handover")
        {
            if (job.Status != InstallationJobStatus.REPORT_SUBMITTED) throw new ConflictException("This report is not awaiting handover confirmation.");
            report.ConfirmedByUserId = actorId; report.ConfirmedAt = now; report.ConfirmationNote = request.Note.Trim();
            job.Status = InstallationJobStatus.OWNER_CONFIRMED;
            await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Installation handover confirmed", $"Installation job #{jobId} is ready for final review.", now, ct);
        }
        else
        {
            if (action is not ("approve" or "return")) throw new BusinessRuleException("Unsupported installation decision.");
            if (action == "approve" && (job.Status != InstallationJobStatus.OWNER_CONFIRMED || report.ConfirmedAt is null)) throw new ConflictException("Current owner handover confirmation is required.");
            if (action == "return" && job.Status is not (InstallationJobStatus.REPORT_SUBMITTED or InstallationJobStatus.OWNER_CONFIRMED)) throw new ConflictException("Only a submitted report can be returned.");
            if (action == "approve" && report.Devices.Any(d => !d.AccessoriesChecked || !d.ConnectivityPassed || !d.OperationPassed)) throw new BusinessRuleException("Resolve failed device checks before approval.");
            report.ReviewedByUserId = actorId; report.ReviewedAt = now; report.ReviewNote = request.Note.Trim(); report.Approved = action == "approve";
            job.Status = action == "approve" ? InstallationJobStatus.APPROVED : InstallationJobStatus.REWORK;
            await WorkflowNotifications.AddForUserAsync(db, job.TechnicianId, action == "approve" ? "Installation approved" : "Installation rework required", $"Review the decision for installation job #{jobId}.", now, ct);
            await WorkflowNotifications.AddForUserAsync(db, installation.OwnerId, "Installation review decision", $"Review the decision for installation job #{jobId}.", now, ct);
        }
        job.Revision++;
        History(job, action == "confirm-handover" ? InstallationJobAction.OWNER_CONFIRMED : action == "approve" ? InstallationJobAction.APPROVED : InstallationJobAction.RETURNED,
            actorId, $"Report #{report.Id}: {request.Note.Trim()}", now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    private async Task<string[]> LockActorAsync(int id, CancellationToken ct)
    {
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").ToArrayAsync(ct);
        return await db.UserRoles.Where(x => x.UserId == id && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
    }
    private async Task<InstallationJob> LockJobAsync(int id, CancellationToken ct) => await db.InstallationJobs.FromSqlInterpolated($"SELECT * FROM installation_jobs WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Installation job not found.");
    private static void CheckRevision(InstallationJob job, int revision)
    {
        if (job.Revision != revision) throw new ConflictException("Job changed. Reload the latest revision.");
    }
    private static void History(InstallationJob job, InstallationJobAction action, int actorId, string note, DateTimeOffset now) =>
        job.History.Add(new() { Action = action, UserId = actorId, TechnicianId = job.TechnicianId, ScheduledAt = job.ScheduledAt, Note = note, CreatedAt = now });
}
