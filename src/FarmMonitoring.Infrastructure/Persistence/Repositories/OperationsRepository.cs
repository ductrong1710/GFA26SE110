using System.Text.Json;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed partial class OperationsRepository(AppDbContext db, TimeProvider clock) : IOperationsRepository
{
    private async Task<string[]> Roles(int actor, CancellationToken ct) => await db.UserRoles
        .Where(x => x.UserId == actor && x.User.IsActive).Select(x => x.Role.Name).ToArrayAsync(ct);
    private async Task Admin(int actor, CancellationToken ct)
    { if (!(await Roles(actor,ct)).Contains(RoleNames.FarmAdministrator)) throw new AccessDeniedException("Administrator access required."); }
    private async Task<ServiceJob> Job(int actor, int id, bool write, CancellationToken ct)
    {
        var roles = await Roles(actor,ct);
        var job = write ? await db.ServiceJobs.FromSqlInterpolated($"SELECT * FROM service_jobs WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.ServiceJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id,ct);
        if (job is null) throw new NotFoundException("Service job not found.");
        if (!roles.Contains(RoleNames.FarmAdministrator) && !(roles.Contains(RoleNames.Technician) && job.TechnicianId == actor))
            throw new AccessDeniedException("Assigned technician or administrator required.");
        return job;
    }
    private static void Revision(ServiceJob job, int revision)
    { if (revision != job.Revision) throw new ConflictException("Job changed. Reload the current revision."); }
    private static void History(ServiceJob job,int actor,ServiceJobAction action,string note,DateTimeOffset now) =>
        job.History.Add(new() {UserId=actor,TechnicianId=job.TechnicianId,Action=action,Note=note,ScheduledAt=job.ScheduledAt,ScheduledEndAt=job.ScheduledEndAt,CreatedAt=now});
    public async Task<object> CalendarAsync(int actor, DateTimeOffset from, DateTimeOffset to, int? technicianId, CancellationToken ct)
    {
        var roles = await Roles(actor,ct); var admin = roles.Contains(RoleNames.FarmAdministrator);
        if (!admin && !roles.Contains(RoleNames.Technician)) throw new AccessDeniedException("Calendar access denied.");
        if (!admin && technicianId.HasValue && technicianId != actor) throw new AccessDeniedException("Own calendar only.");
        technicianId = admin ? technicianId : actor;
        var installations = await db.InstallationJobs.AsNoTracking().Where(x => (!technicianId.HasValue || x.TechnicianId == technicianId) && x.ScheduledAt < to &&
            (x.ScheduledEndAt > x.ScheduledAt ? x.ScheduledEndAt : x.ScheduledAt.AddHours(2)) > from)
            .Select(x => new { x.Id, Type = "INSTALLATION", x.TechnicianId, x.ScheduledAt, EndAt = x.ScheduledEndAt > x.ScheduledAt ? x.ScheduledEndAt : x.ScheduledAt.AddHours(2), Status = x.Status.ToString(), x.InstallationRequest.FarmId, FarmName = x.InstallationRequest.Farm.Name, Address = x.InstallationRequest.Proposal.Survey.FarmLocation, Customer = x.InstallationRequest.Proposal.Consultation.CustomerName, Phone = x.InstallationRequest.Proposal.Consultation.Phone, x.Revision }).ToArrayAsync(ct);
        var services = await db.ServiceJobs.AsNoTracking().Where(x => (!technicianId.HasValue || x.TechnicianId == technicianId) && x.ScheduledAt < to &&
            (x.ScheduledEndAt > x.ScheduledAt ? x.ScheduledEndAt : x.ScheduledAt.AddHours(2)) > from)
            .Select(x => new { x.Id, Type = x.Type.ToString(), x.TechnicianId, x.ScheduledAt, EndAt = x.ScheduledEndAt > x.ScheduledAt ? x.ScheduledEndAt : x.ScheduledAt.AddHours(2), Status = x.Status.ToString(), x.RentalDeviceAssignment.Contract.FarmId, FarmName = x.RentalDeviceAssignment.Contract.InstallationJob.InstallationRequest.Farm.Name, Address = x.RentalDeviceAssignment.Contract.InstallationJob.InstallationRequest.Proposal.Survey.FarmLocation, Customer = x.RentalDeviceAssignment.Contract.InstallationJob.InstallationRequest.Proposal.Consultation.CustomerName, Phone = x.RentalDeviceAssignment.Contract.InstallationJob.InstallationRequest.Proposal.Consultation.Phone, x.Revision }).ToArrayAsync(ct);
        return new { From = from, To = to, TravelBufferMinutes = 30, Installations = installations, Services = services };
    }
    public async Task<object> AvailableAsync(int actor, DateTimeOffset from, DateTimeOffset to, string? excludeType, int? excludeId, CancellationToken ct)
    {
        await Admin(actor,ct);
        if (to - from > TimeSpan.FromHours(24) || excludeType is not (null or "INSTALLATION" or "SERVICE")) throw new ValidationException("Invalid appointment or excluded job type.");
        var techs = await db.Users.AsNoTracking().Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == RoleNames.Technician))
            .OrderBy(x => x.FullName).ThenBy(x => x.Id).Select(x => new { x.Id, x.FullName }).ToArrayAsync(ct);
        var free = new List<object>();
        foreach (var tech in techs)
            if (!await TechnicianCalendar.BusyAsync(db,tech.Id,from,to,excludeType == "INSTALLATION" ? excludeId : null,excludeType == "SERVICE" ? excludeId : null,ct)) free.Add(tech);
        return new { From = from, To = to, Recommended = free.FirstOrDefault(), Technicians = free };
    }
    public async Task<object> LeadsAsync(int actor, DateTimeOffset from, DateTimeOffset to, PageQuery query, CancellationToken ct)
    {
        var roles = await Roles(actor,ct);
        if (!roles.Contains(RoleNames.FarmAdministrator) && !roles.Contains(RoleNames.FarmEngineer)) throw new AccessDeniedException("Consultation list access denied.");
        var rows = db.Consultations.AsNoTracking().Where(x => x.CreatedAt >= from && x.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.CustomerName.Contains(query.Search) || x.Phone.Contains(query.Search));
        return new { Total = await rows.CountAsync(ct), query.Page, query.PageSize, Items = await rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((query.Page-1)*query.PageSize).Take(query.PageSize).Select(x => new { x.Id, x.CustomerName, x.Phone, x.FarmLocation, x.ServiceNeed, x.Content, x.CreatedAt }).ToArrayAsync(ct) };
    }
    public async Task<object> PlansAsync(int actor, int jobId, CancellationToken ct)
    { await Job(actor,jobId,false,ct); return await db.ServicePlans.AsNoTracking().Where(x => x.JobId == jobId).OrderByDescending(x => x.Id).Select(x => new { x.Id, x.JobId, Status = x.Status.ToString(), x.Note, x.ReviewNote, x.TechnicianId, x.ReviewerId, x.CreatedAt, x.ReviewedAt, x.SensorNodeId, x.UavId, x.GatewayId, x.TotalVnd, x.PriceLinesJson, x.EvidenceJson }).ToArrayAsync(ct); }
    public async Task<object> ProposeAsync(int actor, int jobId, ProposeServicePlan request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000 || request.Lines is null || request.Lines.Length > 100)
            throw new ValidationException("Supply note and at most 100 price lines.");
        var targetCount = new[] {request.SensorNodeId,request.UavId,request.GatewayId}.Count(x => x.HasValue);
        if (targetCount > 1) throw new ValidationException("Choose at most one replacement device.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await FlightReservation.LockAsync(db,ct);
        var job = await Job(actor,jobId,true,ct); Revision(job,request.ExpectedRevision);
        if (!(await Roles(actor,ct)).Contains(RoleNames.Technician) || job.TechnicianId != actor) throw new AccessDeniedException("Assigned technician required.");
        if (job.Status is not (ServiceJobStatus.IN_PROGRESS or ServiceJobStatus.REWORK)) throw new ConflictException("Start work before proposing a plan.");
        if (await db.ServicePlans.AnyAsync(x => x.JobId == jobId && x.Status != ServicePlanStatus.REJECTED && x.Status != ServicePlanStatus.APPLIED,ct)) throw new ConflictException("This job already has a pending plan.");
        var old = await db.RentalDeviceAssignments.Include(x => x.Contract).SingleAsync(x => x.Id == job.RentalDeviceAssignmentId,ct);
        var successor = await db.ServiceDeviceReplacements.Where(x => x.JobId == jobId).OrderByDescending(x => x.Id).Select(x => (int?)x.NewAssignmentId).FirstOrDefaultAsync(ct);
        if (successor.HasValue) old = await db.RentalDeviceAssignments.Include(x => x.Contract).SingleAsync(x => x.Id == successor,ct);
        if (old.ReturnedAt != null || old.Contract.Status == RentalStatus.ENDED) throw new ConflictException("Equipment is no longer assigned.");
        if (targetCount == 1)
        {
            if (old.SensorNodeId.HasValue != request.SensorNodeId.HasValue || old.UavId.HasValue != request.UavId.HasValue || old.GatewayId.HasValue != request.GatewayId.HasValue) throw new BusinessRuleException("Replacement must have the same device type.");
            var eligible = request.SensorNodeId is int s ? await db.SensorNodes.AnyAsync(x => x.Id == s && x.IsActive && x.Zone.FarmId == old.Contract.FarmId,ct)
                : request.UavId is int u ? await db.Uavs.AnyAsync(x => x.Id == u && x.IsActive,ct) : await db.Gateways.AnyAsync(x => x.Id == request.GatewayId && x.IsActive,ct);
            if (!eligible || await ReservedAsync(request.SensorNodeId,request.UavId,request.GatewayId,ct) || await db.ServicePlans.AnyAsync(x => (x.Status == ServicePlanStatus.PROPOSED || x.Status == ServicePlanStatus.APPROVED || x.Status == ServicePlanStatus.EXECUTED) && (request.SensorNodeId != null && x.SensorNodeId == request.SensorNodeId || request.UavId != null && x.UavId == request.UavId || request.GatewayId != null && x.GatewayId == request.GatewayId),ct)) throw new ConflictException("Replacement is unavailable.");
        }
        var prices = new List<PlanPriceSnapshot>();
        foreach (var line in request.Lines)
        {
            if (line is null || line.Quantity is < 1 or > 1000 || string.IsNullOrWhiteSpace(line.Reason) || line.Reason.Length > 1000) throw new ValidationException("Each line requires quantity 1-1000 and warranty/charge reason.");
            var price = await db.PublicContent.AsNoTracking().SingleOrDefaultAsync(x => x.Id == line.PublicContentId && x.Published && x.ReplacementPriceVnd != null,ct) ?? throw new BusinessRuleException("Choose a published replacement price.");
            prices.Add(new(price.Id,price.Revision,price.Title,line.Quantity,price.ReplacementPriceVnd!.Value,line.Warranty,line.Reason.Trim()));
        }
        var plan = new ServicePlan { JobId = jobId, OldAssignmentId = old.Id, TechnicianId = actor, SensorNodeId = request.SensorNodeId, UavId = request.UavId, GatewayId = request.GatewayId,
            Note = request.Note.Trim(), CreatedAt = clock.GetUtcNow(), PriceLinesJson = JsonSerializer.Serialize(prices), TotalVnd = prices.Sum(x => x.TotalVnd) };
        if (plan.TotalVnd >= 10000000000000000m) throw new ValidationException("Total exceeds supported amount.");
        db.ServicePlans.Add(plan); job.Revision++;
        History(job,actor,ServiceJobAction.PLAN_PROPOSED,plan.Note,clock.GetUtcNow());
        await WorkflowNotifications.AddForRoleAsync(db,RoleNames.FarmAdministrator,"Service plan awaiting approval",$"Review service plan for job #{jobId}.",clock.GetUtcNow(),ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return await PlansAsync(actor,jobId,ct);
    }
    private Task<bool> ReservedAsync(int? sensor, int? uav, int? gateway, CancellationToken ct) => db.InstallationDeviceAllocations.AnyAsync(x => x.ReleasedAt == null &&
        (sensor != null && x.SensorNodeId == sensor || uav != null && x.UavId == uav || gateway != null && x.GatewayId == gateway),ct);
    public async Task<object> ReviewAsync(int actor, int planId, ReviewServicePlan request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000) throw new ValidationException("Review note required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await FlightReservation.LockAsync(db,ct); await Admin(actor,ct);
        var plan = await db.ServicePlans.SingleOrDefaultAsync(x => x.Id == planId,ct) ?? throw new NotFoundException("Plan not found.");
        var job = await Job(actor,plan.JobId,true,ct); Revision(job,request.ExpectedRevision);
        if (plan.Status != ServicePlanStatus.PROPOSED || job.Status is not (ServiceJobStatus.IN_PROGRESS or ServiceJobStatus.REWORK)) throw new ConflictException("Plan is not awaiting review.");
        plan.Status = request.Approve ? ServicePlanStatus.APPROVED : ServicePlanStatus.REJECTED; plan.ReviewerId = actor; plan.ReviewedAt = clock.GetUtcNow(); plan.ReviewNote = request.Note.Trim(); job.Revision++;
        History(job,actor,request.Approve ? ServiceJobAction.PLAN_APPROVED : ServiceJobAction.PLAN_REJECTED,plan.ReviewNote,clock.GetUtcNow());
        await WorkflowNotifications.AddForUserAsync(db,job.TechnicianId,"Service plan reviewed",$"Plan #{planId}: {plan.Status}.",clock.GetUtcNow(),ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return await PlansAsync(actor,job.Id,ct);
    }
    public async Task<object> ExecuteAsync(int actor, int planId, ExecuteServicePlan request, CancellationToken ct)
    {
        PaperEvidence.Required(request.SignedAgreement,"customer signed agreement");
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await FlightReservation.LockAsync(db,ct);
        var plan = await db.ServicePlans.SingleOrDefaultAsync(x => x.Id == planId,ct) ?? throw new NotFoundException("Plan not found.");
        var job = await Job(actor,plan.JobId,true,ct); Revision(job,request.ExpectedRevision);
        if (!(await Roles(actor,ct)).Contains(RoleNames.Technician) || job.TechnicianId != actor) throw new AccessDeniedException("Assigned technician required.");
        if (plan.Status != ServicePlanStatus.APPROVED || job.Status is not (ServiceJobStatus.IN_PROGRESS or ServiceJobStatus.REWORK)) throw new ConflictException("Admin-approved plan required before execution.");
        if (plan.SensorNodeId.HasValue || plan.UavId.HasValue || plan.GatewayId.HasValue)
        { PaperEvidence.Required(request.OldDeviceImages,"old device serial"); PaperEvidence.Required(request.NewDeviceImages,"new device serial"); }
        else { EvidenceImages.Decode(request.OldDeviceImages); EvidenceImages.Decode(request.NewDeviceImages); }
        plan.Status = ServicePlanStatus.EXECUTED; plan.EvidenceJson = JsonSerializer.Serialize(request); job.Revision++;
        plan.TechnicianId = actor;
        History(job,actor,ServiceJobAction.PLAN_EXECUTED,$"Executed plan #{planId}; signed paper and device evidence attached.",clock.GetUtcNow());
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return await PlansAsync(actor,job.Id,ct);
    }
}
