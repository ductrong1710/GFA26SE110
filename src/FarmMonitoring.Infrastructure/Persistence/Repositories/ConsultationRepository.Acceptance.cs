using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed partial class ConsultationRepository
{
    private const string ExpiredMessage = "Lượt phân công đã hết hạn. Vui lòng liên hệ Admin";
    private const string ExpiredReason = "Quá thời gian tiếp nhận công việc (5 giờ)";

    private IQueryable<ConsultationHistory> AssignmentStarts(int id) =>
        db.Set<ConsultationHistory>().Where(x => x.ConsultationId == id && x.Status == ConsultationStatus.PENDING_ACCEPTANCE &&
            (x.Action == ConsultationAction.ASSIGNED || x.Action == ConsultationAction.REASSIGNED));

    public async Task RespondToAssignmentAsync(int id, int engineerId, RespondToConsultationAssignment request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEngineerAsync(engineerId, ct);
        var row = await LockAsync(id, ct);
        var start = await AssignmentStarts(id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (row.Status != ConsultationStatus.PENDING_ACCEPTANCE || row.EngineerId != engineerId || start?.Id != request.AssignmentId)
        {
            var next = await AssignmentStarts(id).Where(x => x.Id > request.AssignmentId)
                .OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (await AssignmentStarts(id).AnyAsync(x => x.Id == request.AssignmentId, ct) &&
                await db.Set<ConsultationHistory>().AnyAsync(x => x.ConsultationId == id && x.UserId == engineerId &&
                    x.Id > request.AssignmentId && (next == null || x.Id < next) && x.Action == ConsultationAction.ASSIGNMENT_EXPIRED, ct))
                throw new ConflictException(ExpiredMessage);
            if (row.EngineerId != engineerId) throw AccessDeniedException.OutsideScope();
            throw new ConflictException("Lượt phân công không còn hiệu lực.");
        }

        var now = clock.GetUtcNow();
        if (now >= start.CreatedAt + ConsultationAssignmentAttempt.ResponseWindow)
        {
            await CompleteAssignmentAsync(row, engineerId, ConsultationAction.ASSIGNMENT_EXPIRED, ExpiredReason, now, ct);
            await transaction.CommitAsync(ct);
            throw new ConflictException(ExpiredMessage);
        }
        await CompleteAssignmentAsync(row, engineerId,
            request.Accept ? ConsultationAction.ASSIGNMENT_ACCEPTED : ConsultationAction.ASSIGNMENT_REJECTED, request.Reason, now, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ExpireAssignmentsAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow() - ConsultationAssignmentAttempt.ResponseWindow;
        var candidates = await db.Consultations.AsNoTracking()
            .Where(x => x.Status == ConsultationStatus.PENDING_ACCEPTANCE && x.UpdatedAt <= cutoff && x.EngineerId != null)
            .OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id).Select(x => new { x.Id, EngineerId = x.EngineerId!.Value })
            .Take(100).ToArrayAsync(ct);
        foreach (var candidate in candidates)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var recipients = await WorkflowNotifications.RecipientsForRoleAsync(db, RoleNames.FarmAdministrator, ct);
            var foreignKeys = recipients.Append(candidate.EngineerId).Distinct().ToArray();
            // Acquire FK locks before the consultation, in the same order as workflow writers.
            // Leave busy accounts to a later pass instead of waiting while holding other user locks.
            var lockedUsers = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = ANY ({foreignKeys}) ORDER BY id FOR KEY SHARE SKIP LOCKED")
                .AsNoTracking().ToArrayAsync(ct);
            if (lockedUsers.Length != foreignKeys.Length) continue;
            var row = await LockAsync(candidate.Id, ct);
            var now = clock.GetUtcNow();
            if (row.Status == ConsultationStatus.PENDING_ACCEPTANCE && row.EngineerId == candidate.EngineerId &&
                row.UpdatedAt + ConsultationAssignmentAttempt.ResponseWindow <= now)
            {
                await CompleteAssignmentAsync(row, candidate.EngineerId, ConsultationAction.ASSIGNMENT_EXPIRED, ExpiredReason, now, ct, recipients);
                await transaction.CommitAsync(ct);
            }
        }
    }

    private async Task CompleteAssignmentAsync(Consultation row, int engineerId, ConsultationAction action, string? reason,
        DateTimeOffset now, CancellationToken ct, int[]? recipients = null)
    {
        var accepted = action == ConsultationAction.ASSIGNMENT_ACCEPTED;
        row.Status = accepted ? ConsultationStatus.IN_PROGRESS : ConsultationStatus.NEEDS_REASSIGNMENT;
        if (!accepted) row.EngineerId = null;
        AddAssignmentEvent(row, engineerId, action, reason, now);
        var name = await db.Users.Where(x => x.Id == engineerId).Select(x => x.FullName).SingleAsync(ct);
        var message = action switch
        {
            ConsultationAction.ASSIGNMENT_EXPIRED =>
                $"Yêu cầu tư vấn #{row.Id} được phân công cho {name} đã quá thời hạn tiếp nhận 5 giờ. Hệ thống đã tự động hủy lượt phân công. Vui lòng phân công kỹ sư khác.",
            ConsultationAction.ASSIGNMENT_REJECTED =>
                $"Yêu cầu tư vấn #{row.Id} đã bị {name} từ chối. Vui lòng phân công kỹ sư khác.",
            _ => $"Yêu cầu tư vấn #{row.Id} đã được {name} tiếp nhận."
        };
        var subject = accepted ? "Kỹ sư đã tiếp nhận yêu cầu tư vấn" : "Yêu cầu tư vấn cần phân công lại";
        recipients ??= await WorkflowNotifications.RecipientsForRoleAsync(db, RoleNames.FarmAdministrator, ct);
        WorkflowNotifications.AddForUsers(db, recipients, subject, message, now);
        await db.SaveChangesAsync(ct);
    }
}
