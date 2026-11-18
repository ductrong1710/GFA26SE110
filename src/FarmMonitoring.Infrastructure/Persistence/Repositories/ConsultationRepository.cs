using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed partial class ConsultationRepository(AppDbContext db, TimeProvider clock) : IConsultationRepository
{
    public async Task CreateAsync(Consultation consultation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Consultations.Add(consultation);
        await db.SaveChangesAsync(ct);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Yêu cầu tư vấn mới",
            $"Yêu cầu tư vấn #{consultation.Id} đang chờ phân công nhân viên.",
            consultation.CreatedAt, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<PagedResult<ConsultationSummary>> ListAsync(int engineerId, bool administrator, ConsultationQuery query, CancellationToken ct)
    {
        var rows = db.Consultations.AsNoTracking().Where(x => administrator
            ? !query.Inbox || x.EngineerId == null && (x.Status == ConsultationStatus.NEW || x.Status == ConsultationStatus.NEEDS_REASSIGNMENT)
            : x.EngineerId == engineerId);
        if (query.PendingAcceptance) rows = rows.Where(x => x.Status == ConsultationStatus.PENDING_ACCEPTANCE);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.ServiceNeed.Contains(query.Search.Trim()));
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ConsultationSummary(x.Id, x.ServiceNeed, x.Status.ToString(), x.CreatedAt)).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }

    public Task<Consultation?> GetAsync(int id, CancellationToken ct) =>
        db.Consultations.AsNoTracking().Include(x => x.History).SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<Dictionary<int, string>> GetParticipantNamesAsync(int[] ids, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName }).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

    public async Task<PagedResult<ConsultationSummary>> FarmConsultationsAsync(int farmId, int userId, bool administrator, PageQuery query, CancellationToken ct)
    {
        var rows = db.Consultations.AsNoTracking().Where(x => x.FarmId == farmId && (administrator || x.OwnerId == userId));
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ConsultationSummary(x.Id, x.ServiceNeed, x.Status.ToString(), x.CreatedAt)).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }

    public async Task AssignAsync(int id, int administratorId, AssignConsultationRequest request, CancellationToken ct)
    {
        var engineerId = request.EngineerId;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id IN ({administratorId}, {engineerId}) ORDER BY id FOR UPDATE").ToArrayAsync(ct);
        if (!locked.Any(x => x.Id == administratorId && x.IsActive) || !await db.UserRoles.AnyAsync(x => x.UserId == administratorId && x.Role.Name == RoleNames.FarmAdministrator, ct))
            throw new AccessDeniedException("Administrator access required.");
        if (!locked.Any(x => x.Id == engineerId && x.IsActive) || !await db.UserRoles.AnyAsync(x => x.UserId == engineerId && x.Role.Name == RoleNames.FarmEngineer, ct))
            throw new BusinessRuleException("Chọn nhân viên tư vấn đang hoạt động.");
        var row = await LockAsync(id, ct);
        var previousEngineerId = row.EngineerId;
        if (previousEngineerId != request.ExpectedEngineerId || row.Status is not
            (ConsultationStatus.NEW or ConsultationStatus.IN_PROGRESS or ConsultationStatus.NEEDS_REASSIGNMENT))
            throw new ConflictException("Phân công đã thay đổi hoặc hồ sơ đã đóng. Vui lòng tải lại hồ sơ.");
        if (previousEngineerId == engineerId)
            throw new ConflictException("Kỹ sư này đang phụ trách hồ sơ.");
        var returns = await ReturnHistory(id, ct);
        if (PendingReturn(returns) != null)
            throw new ConflictException("Hãy xử lý yêu cầu trả hồ sơ trước khi phân công lại.");
        if (PreviouslyReturned(returns).ContainsKey(engineerId))
            throw new ConflictException("Kỹ sư này đã được duyệt trả lại hồ sơ.");
        if (await OpenConsultations().CountAsync(x => x.EngineerId == engineerId, ct) >= AssignmentCapacity)
            throw new ConflictException("Kỹ sư đã đạt giới hạn công việc.");
        if (previousEngineerId.HasValue) await EnsureNoUnresolvedPayment(id, ct);
        var formerEngineerId = previousEngineerId;
        if (row.Status == ConsultationStatus.NEEDS_REASSIGNMENT)
            formerEngineerId = await db.Set<ConsultationHistory>().Where(x => x.ConsultationId == id &&
                (x.Action == ConsultationAction.ASSIGNMENT_EXPIRED || x.Action == ConsultationAction.ASSIGNMENT_REJECTED))
                .OrderByDescending(x => x.Id).Select(x => x.UserId).FirstOrDefaultAsync(ct);
        var now = clock.GetUtcNow();
        row.EngineerId = engineerId;
        row.Status = ConsultationStatus.PENDING_ACCEPTANCE;
        row.UpdatedAt = now;
        row.History.Add(new() { UserId = administratorId, Action = formerEngineerId == null ? ConsultationAction.ASSIGNED : ConsultationAction.REASSIGNED, Status = row.Status,
            Notes = formerEngineerId == null ? $"Nhân viên tư vấn #{engineerId}" : $"Chuyển từ kỹ sư #{formerEngineerId} sang kỹ sư #{engineerId}", CreatedAt = now });
        await WorkflowNotifications.AddForUserAsync(db, engineerId, "Yêu cầu tư vấn được phân công",
            $"Bạn được phân công xử lý yêu cầu tư vấn #{id}. Vui lòng phản hồi trong vòng 5 giờ.", now, ct);
        if (previousEngineerId is int previous)
            await WorkflowNotifications.AddForUserAsync(db, previous, "Yêu cầu tư vấn đã được chuyển",
                $"Hồ sơ tư vấn #{id} đã được chuyển sang kỹ sư khác.", now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task FollowUpAsync(int id, int engineerId, FollowUpRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEngineerAsync(engineerId, ct);
        var row = await LockAsync(id, ct);
        if (row.EngineerId != engineerId) throw new AccessDeniedException("Only the assigned engineer can update this consultation.");
        if (row.Status != ConsultationStatus.IN_PROGRESS) throw new ConflictException("Consultation is not in progress.");
        row.UpdatedAt = now;
        row.History.Add(new() { UserId = engineerId, Action = ConsultationAction.FOLLOW_UP, Status = row.Status,
            Notes = request.Notes.Trim(), ContactResult = request.ContactResult.Trim(), NextAction = request.NextAction.Trim(), CreatedAt = now });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Consultation> LockAsync(int id, CancellationToken ct) =>
        await db.Consultations.FromSqlInterpolated($"SELECT * FROM consultations WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Consultation not found.");

    public async Task AddSurveyAsync(FarmSurvey survey, int expectedRevision, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEngineerAsync(survey.EngineerId, ct);
        var consultation = await LockAsync(survey.ConsultationId, ct);
        if (consultation.EngineerId != survey.EngineerId)
            throw new AccessDeniedException("Only the assigned engineer can record a survey.");
        var visit = await db.Set<SiteVisitProposal>().Where(x => x.ConsultationId == consultation.Id)
            .OrderByDescending(x => x.Revision).Select(x => new { x.Channel, x.ReviewStatus, x.ScheduledAt }).FirstOrDefaultAsync(ct);
        if ((consultation.Channel == "ON_SITE" || visit is { Channel: "ON_SITE", ReviewStatus: "PENDING" }) && visit?.ScheduledAt is null)
            throw new ConflictException("Confirm the on-site visit schedule before recording the survey.");
        if (consultation.EngineerId != survey.EngineerId) throw new AccessDeniedException("Only the assigned engineer can record a survey.");
        if (consultation.Status != ConsultationStatus.IN_PROGRESS) throw new ConflictException("Consultation is not in progress.");
        if (await db.DeviceProposals.AnyAsync(x => x.ConsultationId == survey.ConsultationId && x.Status == ProposalStatus.WAITING_ADMIN, ct))
            throw new ConflictException("Wait for administrator review before changing submitted farm information.");
        var latest = await db.FarmSurveys.Where(x => x.ConsultationId == survey.ConsultationId).MaxAsync(x => (int?)x.Revision, ct) ?? 0;
        if (latest != expectedRevision) throw new ConflictException("Survey changed. Reload the latest revision.");
        survey.Revision = latest + 1;
        db.FarmSurveys.Add(survey);
        consultation.UpdatedAt = survey.CreatedAt;
        consultation.History.Add(new() { UserId = survey.EngineerId, Action = ConsultationAction.SURVEY_RECORDED,
            Status = consultation.Status, CreatedAt = survey.CreatedAt, Notes = $"Survey revision {survey.Revision} recorded." });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<PagedResult<SurveySummary>> ListSurveysAsync(int consultationId, PageQuery query, CancellationToken ct)
    {
        var rows = db.FarmSurveys.AsNoTracking().Where(x => x.ConsultationId == consultationId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.FarmName.Contains(query.Search.Trim()));
        var total = await rows.CountAsync(ct);
        var result = await rows.OrderByDescending(x => x.Revision).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SurveySummary(x.Id, x.Revision, x.FarmName, x.EngineerId, x.CreatedAt)).ToArrayAsync(ct);
        return new(result, total, query.Page, query.PageSize);
    }

    public Task<FarmSurvey?> GetSurveyAsync(int consultationId, int surveyId, CancellationToken ct) =>
        db.FarmSurveys.AsNoTracking().Include(x => x.Zones).Include(x => x.Images).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == surveyId && x.ConsultationId == consultationId, ct);

    private async Task LockEngineerAsync(int id, CancellationToken ct)
    {
        // Serialize with role/status changes so stale eligibility cannot update work.
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is not { IsActive: true } || !await db.UserRoles.AnyAsync(x => x.UserId == id && x.Role.Name == RoleNames.FarmEngineer, ct))
            throw new AccessDeniedException("Agricultural Engineer access required.");
    }

    public async Task BindCustomerAsync(int id, int administratorId, CustomerFarmRequest request, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Stable user lock ordering also serializes with account eligibility changes.
        var locked = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id IN ({administratorId}, {request.OwnerId}) ORDER BY id FOR UPDATE").ToArrayAsync(ct);
        if (!locked.Any(x => x.Id == administratorId && x.IsActive) || !await db.UserRoles.AnyAsync(x => x.UserId == administratorId && x.Role.Name == RoleNames.FarmAdministrator, ct))
            throw new AccessDeniedException("Administrator access required.");
        if (!locked.Any(x => x.Id == request.OwnerId && x.IsActive) || !await db.UserRoles.AnyAsync(x => x.UserId == request.OwnerId && x.Role.Name == RoleNames.FarmOwner, ct))
            throw new BusinessRuleException("Choose an active Farm Owner assigned to the farm.");
        var membership = await db.UserFarms.FromSqlInterpolated($"SELECT * FROM user_farms WHERE user_id = {request.OwnerId} AND farm_id = {request.FarmId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (membership is null) throw new BusinessRuleException("The customer must already be a member of the selected farm.");
        var row = await LockAsync(id, ct);
        if (row.OwnerId == request.OwnerId && row.FarmId == request.FarmId) return;
        if (row.OwnerId.HasValue || row.Status != ConsultationStatus.IN_PROGRESS)
            throw new ConflictException("Customer farm can only be linked once while consultation is in progress.");
        row.OwnerId = request.OwnerId; row.FarmId = request.FarmId; row.UpdatedAt = now;
        row.History.Add(new() { Action = ConsultationAction.CUSTOMER_BOUND, UserId = administratorId, Status = row.Status,
            CreatedAt = now, Notes = $"Linked owner #{request.OwnerId} and farm #{request.FarmId}." });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
