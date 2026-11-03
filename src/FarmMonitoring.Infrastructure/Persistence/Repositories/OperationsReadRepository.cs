using FarmMonitoring.Application.Common;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;
public sealed partial class OperationsRepository
{
    public async Task<object> DashboardAsync(int actor, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        await Admin(actor,ct);
        var offset = TimeSpan.FromHours(7);
        var installs = await db.InstallationJobs.AsNoTracking().Where(x => x.CreatedAt >= from && x.CreatedAt < to).Select(x => new { x.CreatedAt, Status = x.Status.ToString() }).ToArrayAsync(ct);
        var services = await db.ServiceJobs.AsNoTracking().Where(x => x.CreatedAt >= from && x.CreatedAt < to).Select(x => new { x.CreatedAt, Type = x.Type.ToString(), Status = x.Status.ToString() }).ToArrayAsync(ct);
        var missions = await db.Missions.AsNoTracking().Where(x => x.CreatedAt >= from && x.CreatedAt < to).Select(x => new { x.CreatedAt, Status = x.Status.ToString() }).ToArrayAsync(ct);
        var alerts = await db.Alerts.AsNoTracking().Where(x => x.OpenedAt >= from && x.OpenedAt < to).Select(x => new { x.OpenedAt, x.AlertType, Status = x.Status.ToString() }).ToArrayAsync(ct);
        var crops = await db.CropConditionReports.AsNoTracking().Where(x => x.ReportedAt >= from && x.ReportedAt < to).Select(x => x.ReportedAt).ToArrayAsync(ct);
        var incidents = await db.DeviceIncidents.AsNoTracking().Where(x => x.ReportedAt >= from && x.ReportedAt < to).Select(x => x.ReportedAt).ToArrayAsync(ct);
        var work = installs.Select(x => new { Date = DateOnly.FromDateTime(x.CreatedAt.ToOffset(offset).Date), Type = "INSTALLATION", x.Status })
            .Concat(services.Select(x => new { Date = DateOnly.FromDateTime(x.CreatedAt.ToOffset(offset).Date), x.Type, x.Status }));
        var now = clock.GetUtcNow();
        return new {
            From = from, To = to, TimeZone = "Asia/Ho_Chi_Minh", WorkBasis = "CreatedAt with current status; not historical transition counts",
            Work = work.GroupBy(x => new { x.Date,x.Type,x.Status }).Select(g => new { g.Key.Date,g.Key.Type,g.Key.Status,Count = g.Count() }).ToArray(),
            Alerts = alerts.GroupBy(x => new { Date = DateOnly.FromDateTime(x.OpenedAt.ToOffset(offset).Date),x.AlertType,x.Status }).Select(g => new {g.Key.Date,g.Key.AlertType,g.Key.Status,Count=g.Count()}).ToArray(),
            Missions = missions.GroupBy(x => new { Date = DateOnly.FromDateTime(x.CreatedAt.ToOffset(offset).Date),x.Status }).Select(g => new {g.Key.Date,g.Key.Status,Count=g.Count()}).ToArray(),
            Reports = crops.Select(x => new {Date=DateOnly.FromDateTime(x.ToOffset(offset).Date),Type="CROP"}).Concat(incidents.Select(x => new {Date=DateOnly.FromDateTime(x.ToOffset(offset).Date),Type="DEVICE"}))
                .GroupBy(x => new {x.Date,x.Type}).Select(g => new {g.Key.Date,g.Key.Type,Count=g.Count()}).ToArray(),
            InstallationPipeline = await db.InstallationRequests.AsNoTracking().Where(x => x.Status == InstallationRequestStatus.APPROVED && !db.InstallationJobs.Any(j => j.InstallationRequestId == x.Id))
                .Select(x => new {x.Id,x.FarmId,Status="WAITING_ASSIGNMENT"}).ToArrayAsync(ct),
            RentalCounts = await db.RentalContracts.AsNoTracking().GroupBy(x => x.Status).Select(g => new {Status=g.Key.ToString(),Count=g.Count()}).ToArrayAsync(ct),
            RentalsEndingInPeriod = await db.RentalContracts.AsNoTracking().Where(x => x.EndAt >= from && x.EndAt < to && x.Status != RentalStatus.ENDED)
                .OrderBy(x => x.EndAt).Select(x => new {x.Id,x.FarmId,x.OwnerId,x.StartAt,x.EndAt,Status=x.Status.ToString()}).ToArrayAsync(ct),
            Maintenance = await db.ServiceJobs.AsNoTracking().Where(x => x.Type == ServiceJobType.MAINTENANCE && x.ScheduledAt >= from && x.ScheduledAt < to)
                .Select(x => new {x.Id,x.TechnicianId,x.ScheduledAt,Status=x.Status.ToString(),Overdue=x.ScheduledAt < now && x.Status != ServiceJobStatus.CLOSED}).ToArrayAsync(ct),
            ConsultationRequests = await db.Consultations.CountAsync(x => x.CreatedAt >= from && x.CreatedAt < to,ct),
            RecentConsultationRequests = await db.Consultations.AsNoTracking().Where(x => x.CreatedAt >= from && x.CreatedAt < to).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(5)
                .Select(x => new {x.Id,x.CustomerName,x.Phone,x.ServiceNeed,x.CreatedAt}).ToArrayAsync(ct)
        };
    }
    public async Task<object> DeviceHistoryAsync(int actor, string code, PageQuery query, CancellationToken ct)
    {
        await Admin(actor,ct);
        var rows = db.RentalDeviceAssignments.AsNoTracking().Where(x => x.Code == code);
        var assignments = await rows.OrderByDescending(x => x.Id).Skip((query.Page-1)*query.PageSize).Take(query.PageSize)
            .Select(x => new {x.Id,x.Code,x.Name,x.ContractId,x.Contract.FarmId,x.InstallationLocation,x.ReturnedAt,x.InstallationAllocationId}).ToArrayAsync(ct);
        var ids = assignments.Select(x => x.Id).ToArray();
        var allocationIds = assignments.Select(a => a.InstallationAllocationId).ToArray();
        var installationIds = await db.InstallationDeviceAllocations.Where(x => allocationIds.Contains(x.Id)).Select(x => x.JobId).ToArrayAsync(ct);
        return new { Total = await rows.CountAsync(ct), query.Page, query.PageSize, Assignments = assignments,
            Installations = await db.Set<InstallationJobHistory>().AsNoTracking().Where(x => installationIds.Contains(x.JobId)).OrderBy(x => x.CreatedAt)
                .Select(x => new {x.JobId,Action=x.Action.ToString(),x.UserId,x.TechnicianId,x.CreatedAt,x.Note}).ToArrayAsync(ct),
            Reports = await db.ServiceReports.AsNoTracking().Where(x => ids.Contains(x.Job.RentalDeviceAssignmentId) || x.Replacement != null && (ids.Contains(x.Replacement.OldAssignmentId) || ids.Contains(x.Replacement.NewAssignmentId)))
                .OrderByDescending(x => x.CreatedAt).Select(x => new {x.Id,x.JobId,x.TechnicianId,x.CreatedAt,x.Cause,x.Work,x.Components,x.TestResults,x.ConnectivityPassed,x.OperationPassed,x.ConfirmedAt,x.Approved,x.ReviewedAt,x.ReviewNote}).ToArrayAsync(ct),
            Replacements = await db.ServiceDeviceReplacements.AsNoTracking().Where(x => x.OldDeviceCode == code || x.NewDeviceCode == code).OrderBy(x => x.CreatedAt)
                .Select(x => new {x.Id,x.JobId,x.OldDeviceCode,x.NewDeviceCode,x.TechnicianId,x.CreatedAt,x.Note}).ToArrayAsync(ct)
        };
    }
    public async Task<object> InstalledAsync(int actor, string jobType, int jobId, PageQuery query, CancellationToken ct)
    {
        var roles = await Roles(actor,ct); var admin = roles.Contains(RoleNames.FarmAdministrator);
        int farmId;
        if (jobType == "service")
        {
            var service = await Job(actor,jobId,false,ct);
            farmId = (await db.RentalDeviceAssignments.Include(x => x.Contract).SingleAsync(x => x.Id == service.RentalDeviceAssignmentId,ct)).Contract.FarmId;
        }
        else if (jobType == "installation")
        {
            var job = await db.InstallationJobs.AsNoTracking().Include(x => x.InstallationRequest).SingleOrDefaultAsync(x => x.Id == jobId,ct) ?? throw new NotFoundException("Job not found.");
            if (!admin && !(roles.Contains(RoleNames.Technician) && job.TechnicianId == actor)) throw new AccessDeniedException("Assigned technician required.");
            farmId = job.InstallationRequest.FarmId;
        }
        else throw new FluentValidation.ValidationException("Choose service or installation.");
        var rows = db.RentalDeviceAssignments.AsNoTracking().Where(x => x.Contract.FarmId == farmId && x.ReturnedAt == null && x.Contract.Status != RentalStatus.ENDED);
        return new { Total=await rows.CountAsync(ct),query.Page,query.PageSize,Items=await rows.OrderBy(x=>x.Id).Skip((query.Page-1)*query.PageSize).Take(query.PageSize)
            .Select(x=>new {x.Id,x.Code,x.Name,x.SensorNodeId,x.UavId,x.GatewayId,x.InstallationLocation,
                History= db.ServiceReports.Where(r=>r.Job.RentalDeviceAssignmentId == x.Id).OrderByDescending(r=>r.CreatedAt).Select(r=>new {r.JobId,r.CreatedAt,r.Cause,r.Components,r.TestResults,r.TechnicianId,r.Approved}).ToArray()}).ToArrayAsync(ct) };
    }
}
