using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed class ServiceReportRepository(AppDbContext db, ServiceReplacementRepository replacements) : IServiceReportRepository
{
    public ServiceReportRepository(AppDbContext db) : this(db,new ServiceReplacementRepository(db)) { }
    public async Task<PagedResult<ServiceReportSummary>> ListAsync(int jobId, PageQuery query, CancellationToken ct)
    {
        var rows = db.ServiceReports.AsNoTracking().Where(x => x.JobId == jobId);
        var count = await rows.CountAsync(ct);
        return new(await rows.OrderByDescending(x => x.Revision).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ServiceReportSummary(x.Id, x.Revision, x.TechnicianId, x.CreatedAt, x.Approved)).ToArrayAsync(ct), count, query.Page, query.PageSize);
    }
    public Task<ServiceReport?> GetAsync(int jobId, int id, CancellationToken ct) => db.ServiceReports.AsNoTracking().Include(x => x.Replacement).Include(x => x.Images).SingleOrDefaultAsync(x => x.Id == id && x.JobId == jobId, ct);
    public async Task<ServiceReport> SubmitAsync(int jobId, int actorId, ServiceReportRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var roles = await LockActorAsync(actorId, ct);
        var job = await LockJobAsync(jobId, ct);
        if (!roles.Contains(RoleNames.Technician) || job.TechnicianId != actorId) throw new AccessDeniedException("Assigned Technician access required.");
        CheckRevision(job, request.ExpectedRevision);
        if (job.Status is not (ServiceJobStatus.IN_PROGRESS or ServiceJobStatus.REWORK)) throw new ConflictException("Start work or resolve requested rework before submitting a report.");
        var plan = await db.ServicePlans.Where(x => x.JobId == jobId && x.Status != ServicePlanStatus.REJECTED && x.Status != ServicePlanStatus.APPLIED).SingleOrDefaultAsync(ct);
        if (plan is null || plan.Status != ServicePlanStatus.EXECUTED) throw new ConflictException("An approved, executed service plan with signed paper evidence is required.");
        PaperEvidence.Required(request.SignedHandover,"signed repair handover");
        var report = new ServiceReport
        {
            Replacement = await db.ServiceDeviceReplacements.Where(x => x.JobId == jobId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct),
            JobId = jobId, TechnicianId = actorId, CreatedAt = now,
            Revision = (await db.ServiceReports.Where(x => x.JobId == jobId).MaxAsync(x => (int?)x.Revision, ct) ?? 0) + 1,
            Cause = request.Cause.Trim(), Work = request.Work.Trim(), Components = request.Components.Trim(), Notes = request.Notes.Trim(),
            TestResults = request.TestResults.Trim(), ConnectivityPassed = request.ConnectivityPassed!.Value, OperationPassed = request.OperationPassed!.Value,
            Images = EvidenceImages.Decode(request.Images),
            PaperworkJson = System.Text.Json.JsonSerializer.Serialize(request.SignedHandover)
        };
        db.ServiceReports.Add(report); job.Status = ServiceJobStatus.REPORT_SUBMITTED; job.Revision++;
        History(job, ServiceJobAction.REPORT_SUBMITTED, actorId, $"Service report revision {report.Revision} submitted.", now);
        var assignment = await AssignmentAsync(job, ct);
        await NotifyOwnerAsync(assignment, "Service operation confirmation required", $"Review the latest report for service job #{jobId}.", now, ct);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Service report submitted", $"Service job #{jobId} awaits operation confirmation and review.", now, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return report;
    }
    public async Task DecideAsync(int jobId, int actorId, string action, ServiceReportDecisionRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db,ct);
        var roles = await LockActorAsync(actorId, ct);
        var job = await LockJobAsync(jobId, ct);
        var assignment = await AssignmentAsync(job, ct);
        if (action == "confirm-operation")
        {
            if (!roles.Contains(RoleNames.FarmOwner) || assignment.Contract.OwnerId != actorId ||
                !await FarmAccessQuery.AssignedFarmIds(db, actorId).ContainsAsync(assignment.Contract.FarmId, ct))
                throw new AccessDeniedException("Linked Farm Owner access required.");
        }
        else if (!roles.Contains(RoleNames.FarmAdministrator)) throw new AccessDeniedException("Administrator access required.");
        CheckRevision(job, request.ExpectedRevision);
        var report = await db.ServiceReports.Where(x => x.JobId == jobId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        if (report is null || report.Id != request.ReportId) throw new ConflictException("Review the latest report version.");
        if (action == "confirm-operation")
        {
            if (job.Status != ServiceJobStatus.REPORT_SUBMITTED) throw new ConflictException("This report is not awaiting operation confirmation.");
            report.ConfirmedByUserId = actorId; report.ConfirmedAt = now; report.ConfirmationNote = request.Note.Trim();
            job.Status = ServiceJobStatus.OWNER_CONFIRMED;
            await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Service operation confirmed", $"Service job #{jobId} is ready for final review.", now, ct);
        }
        else
        {
            if (action is not ("approve" or "return")) throw new BusinessRuleException("Unsupported service decision.");
            if (action == "approve" && (job.Status != ServiceJobStatus.OWNER_CONFIRMED || report.PaperworkJson is null)) throw new ConflictException("Owner confirmation and signed handover are required.");
            if (action == "return" && job.Status is not (ServiceJobStatus.REPORT_SUBMITTED or ServiceJobStatus.OWNER_CONFIRMED)) throw new ConflictException("Only a submitted report can be returned.");
            if (action == "approve" && (!report.ConnectivityPassed || !report.OperationPassed)) throw new BusinessRuleException("Resolve failed device checks before approval.");
            if (action == "approve")
            {
                await replacements.ApplyPendingAsync(jobId,actorId,now,ct);
                report.Replacement = await db.ServiceDeviceReplacements.Where(x => x.JobId == jobId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            }
            report.ReviewedByUserId = actorId; report.ReviewedAt = now; report.ReviewNote = request.Note.Trim(); report.Approved = action == "approve";
            job.Status = action == "approve" ? ServiceJobStatus.CLOSED : ServiceJobStatus.REWORK;
            if (action == "approve" && job.IncidentId is int incidentId)
                (await db.DeviceIncidents.SingleAsync(x => x.Id == incidentId, ct)).Status = DeviceIncidentStatus.RESOLVED;
            await WorkflowNotifications.AddForUserAsync(db, job.TechnicianId, action == "approve" ? "Service job closed" : "Service rework required", $"Review the decision for service job #{jobId}.", now, ct);
            await NotifyOwnerAsync(assignment, "Service review decision", $"Review the decision for service job #{jobId}.", now, ct);
        }
        job.Revision++;
        History(job, action == "confirm-operation" ? ServiceJobAction.OWNER_CONFIRMED : action == "approve" ? ServiceJobAction.CLOSED : ServiceJobAction.RETURNED,
            actorId, $"Report #{report.Id}: {request.Note.Trim()}", now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    private Task<RentalDeviceAssignment> AssignmentAsync(ServiceJob job, CancellationToken ct) => db.RentalDeviceAssignments.Include(x => x.Contract).SingleAsync(x => x.Id == job.RentalDeviceAssignmentId, ct);
    private async Task NotifyOwnerAsync(RentalDeviceAssignment assignment, string title, string message, DateTimeOffset now, CancellationToken ct)
    {
        var ownerId = assignment.Contract.OwnerId;
        if (await FarmAccessQuery.AssignedFarmIds(db, ownerId).ContainsAsync(assignment.Contract.FarmId, ct))
            await WorkflowNotifications.AddForUserAsync(db, ownerId, title, message, now, ct);
    }
    private async Task<string[]> LockActorAsync(int id, CancellationToken ct)
    {
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").ToArrayAsync(ct);
        return await db.UserRoles.Where(x => x.UserId == id && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
    }
    private async Task<ServiceJob> LockJobAsync(int id, CancellationToken ct) => await db.ServiceJobs.FromSqlInterpolated($"SELECT * FROM service_jobs WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Service job not found.");
    private static void CheckRevision(ServiceJob job, int revision)
    {
        if (job.Revision != revision) throw new ConflictException("Job changed. Reload the latest revision.");
    }
    private static void History(ServiceJob job, ServiceJobAction action, int actorId, string note, DateTimeOffset now) =>
        job.History.Add(new() { Action = action, UserId = actorId, TechnicianId = job.TechnicianId, ScheduledAt = job.ScheduledAt, Note = note, CreatedAt = now });
}
