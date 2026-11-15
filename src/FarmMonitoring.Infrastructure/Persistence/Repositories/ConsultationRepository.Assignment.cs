using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed partial class ConsultationRepository
{
    private const int AssignmentCapacity = 10;

    private IQueryable<Consultation> OpenConsultations() => db.Consultations.Where(row =>
        row.Status == ConsultationStatus.NEW || row.Status == ConsultationStatus.IN_PROGRESS);

    public async Task<IReadOnlyList<ConsultationAssignmentCandidate>> ListAssignmentCandidatesAsync(int id, CancellationToken ct)
    {
        var lead = await db.Consultations.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy yêu cầu tư vấn.");

        var excluded = PreviouslyReturned(await ReturnHistory(id, ct));
        var open = OpenConsultations();
        var engineers = await db.Users.Where(user => user.IsActive && user.UserRoles.Any(role => role.Role.Name == RoleNames.FarmEngineer))
            .Select(user => new { user.Id, user.FullName, OpenCount = open.Count(row => row.EngineerId == user.Id) })
            .OrderBy(user => user.OpenCount).ThenBy(user => user.FullName).ThenBy(user => user.Id).ToArrayAsync(ct);

        return engineers.Select(user => new ConsultationAssignmentCandidate(user.Id, user.FullName, user.OpenCount,
            AssignmentCapacity, user.Id == lead.EngineerId ? "Đang phụ trách hồ sơ" : excluded.GetValueOrDefault(user.Id) ??
                (user.OpenCount >= AssignmentCapacity ? "Đã đạt giới hạn công việc" : null))).ToArray();
    }

    public async Task RequestReturnAsync(int id, int engineerId, string reason, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEngineerAsync(engineerId, ct);
        var row = await LockAssignedOpen(id, engineerId, ct);
        if (PendingReturn(await ReturnHistory(id, ct)) != null)
            throw new ConflictException("Hồ sơ đã có yêu cầu trả lại đang chờ duyệt.");

        AddAssignmentEvent(row, engineerId, ConsultationAction.RETURN_REQUESTED, reason, now);
        await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Yêu cầu trả hồ sơ tư vấn",
            $"Kỹ sư đã xin trả hồ sơ #{id}. Mở hồ sơ để xem lý do và quyết định.", now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ReviewReturnAsync(int id, int adminId, ConsultationReturnDecision decision, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var admin = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {adminId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (admin is not { IsActive: true } || !await db.UserRoles.AnyAsync(role => role.UserId == adminId && role.Role.Name == RoleNames.FarmAdministrator, ct))
            throw AccessDeniedException.OutsideScope();
        var row = await LockAsync(id, ct);
        var request = PendingReturn(await ReturnHistory(id, ct));
        if (request is null || request.Id != decision.RequestId || row.Status != ConsultationStatus.IN_PROGRESS ||
            row.EngineerId is not int engineerId || request.UserId != engineerId)
            throw new ConflictException("Yêu cầu trả hồ sơ đã được xử lý hoặc không còn hiệu lực.");

        if (decision.Approve)
        {
            await EnsureNoUnresolvedPayment(id, ct);
            row.EngineerId = null;
            row.Status = ConsultationStatus.NEW;
        }
        AddAssignmentEvent(row, adminId, decision.Approve ? ConsultationAction.RETURN_APPROVED : ConsultationAction.RETURN_REJECTED, null, now);
        await WorkflowNotifications.AddForUserAsync(db, engineerId, decision.Approve ? "Đã duyệt trả hồ sơ" : "Đã từ chối trả hồ sơ",
            $"Quản trị viên đã xử lý yêu cầu trả hồ sơ tư vấn #{id}.", now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task CloseAsync(int id, int engineerId, ConsultationStatus status, string? reason, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEngineerAsync(engineerId, ct);
        var row = await LockAssignedOpen(id, engineerId, ct);
        if (PendingReturn(await ReturnHistory(id, ct)) != null)
            throw new ConflictException("Hãy chờ quản trị viên xử lý yêu cầu trả hồ sơ.");
        await EnsureNoUnresolvedPayment(id, ct);
        row.Status = status;
        AddAssignmentEvent(row, engineerId, ConsultationAction.CLOSED, reason, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Consultation> LockAssignedOpen(int id, int engineerId, CancellationToken ct)
    {
        var row = await LockAsync(id, ct);
        if (row.EngineerId != engineerId) throw AccessDeniedException.OutsideScope();
        if (row.Status != ConsultationStatus.IN_PROGRESS) throw new ConflictException("Hồ sơ không còn đang xử lý.");
        return row;
    }

    private async Task EnsureNoUnresolvedPayment(int id, CancellationToken ct)
    {
        var unresolved = await db.Set<SiteVisitProposal>().Where(p => p.ConsultationId == id)
            .SelectMany(p => p.Transactions).AnyAsync(t => t.Status == "PENDING" || t.Status == "RECONCILIATION_REQUIRED", ct);
        if (unresolved) throw new ConflictException("Cần xác minh kết quả thanh toán VNPay trước khi trả hoặc đóng hồ sơ.");
    }

    private Task<ConsultationHistory[]> ReturnHistory(int id, CancellationToken ct) =>
        db.Set<ConsultationHistory>().AsNoTracking().Where(entry => entry.ConsultationId == id &&
            (entry.Action == ConsultationAction.RETURN_REQUESTED || entry.Action == ConsultationAction.RETURN_APPROVED ||
             entry.Action == ConsultationAction.RETURN_REJECTED)).OrderBy(entry => entry.Id).ToArrayAsync(ct);

    private static ConsultationHistory? PendingReturn(ConsultationHistory[] history) =>
        history.LastOrDefault() is { Action: ConsultationAction.RETURN_REQUESTED } request ? request : null;

    private static Dictionary<int, string> PreviouslyReturned(ConsultationHistory[] history)
    {
        var reasons = new Dictionary<int, string>();
        for (var index = 1; index < history.Length; index++)
        {
            if (history[index].Action == ConsultationAction.RETURN_APPROVED &&
                history[index - 1] is { Action: ConsultationAction.RETURN_REQUESTED, UserId: int id, Notes: { } reason })
                reasons[id] = reason;
        }
        return reasons;
    }

    private static void AddAssignmentEvent(Consultation row, int actor, ConsultationAction action, string? reason, DateTimeOffset now)
    {
        row.UpdatedAt = now;
        row.History.Add(new() { UserId = actor, Action = action, Notes = reason, Status = row.Status, CreatedAt = now });
    }
}
