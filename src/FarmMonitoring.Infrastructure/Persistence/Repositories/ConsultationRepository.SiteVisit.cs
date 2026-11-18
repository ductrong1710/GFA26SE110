using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed partial class ConsultationRepository : ISiteVisitRepository
{
    public async Task<T> WithVisitAsync<T>(int id, int actor, VisitAccess access, bool write,
        Func<Consultation, List<SiteVisitProposal>, bool, T> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actor} FOR SHARE").SingleOrDefaultAsync(ct);
        var roles = await db.UserRoles.Where(x => x.UserId == actor).Select(x => x.Role.Name).ToListAsync(ct);
        var admin = roles.Contains(RoleNames.FarmAdministrator);
        var engineer = roles.Contains(RoleNames.FarmEngineer);
        if (user is not { IsActive: true } || (!admin && !engineer) || (access == VisitAccess.Administrator && !admin)
            || (access == VisitAccess.Engineer && !engineer)) throw AccessDeniedException.OutsideScope();
        var lead = await LockAsync(id, ct);
        if ((access == VisitAccess.Engineer || !admin) && lead.EngineerId != actor) throw AccessDeniedException.OutsideScope();
        if (write && lead.Status != ConsultationStatus.IN_PROGRESS) throw new ConflictException("Consultation is not in progress.");

        var revisions = await db.Set<SiteVisitProposal>().Include(x => x.Payment).Include(x => x.PayOsTransactions).Where(x => x.ConsultationId == id)
            .OrderBy(x => x.Revision).ToListAsync(ct);
        if (admin && revisions.LastOrDefault()?.Payment is { } payment)
            await db.Entry(payment).Reference(x => x.Bill).LoadAsync(ct);
        var count = revisions.Count;
        var previousReview = revisions.LastOrDefault()?.ReviewStatus;
        var previousSchedule = revisions.LastOrDefault()?.ScheduledAt;
        var result = action(lead, revisions, admin);
        if (write)
        {
            if (revisions.Count > count)
            {
                db.Add(revisions[^1]);
                await WorkflowNotifications.AddForRoleAsync(db, RoleNames.FarmAdministrator, "Báo cáo tư vấn chờ duyệt",
                    $"Yêu cầu tư vấn #{id} có báo cáo mới cần xem xét.", lead.UpdatedAt!.Value, ct);
            }
            else if (lead.EngineerId is int assigned && previousSchedule == null && revisions.LastOrDefault()?.ScheduledAt != null)
            {
                var appointment = revisions[^1].ProposedAt!.Value.ToOffset(TimeSpan.FromHours(7));
                await WorkflowNotifications.AddForUserAsync(db, assigned, "Đã chốt lịch tư vấn tại vườn",
                    $"Yêu cầu #{id}: {appointment:dd/MM/yyyy HH:mm} (giờ Việt Nam). Mở yêu cầu để xem địa chỉ và lộ trình.", lead.UpdatedAt!.Value, ct);
            }
            else if (lead.EngineerId is int assignedForReview && previousReview == "PENDING" && revisions[^1].ReviewStatus != previousReview)
            {
                var approved = revisions[^1].ReviewStatus == "APPROVED";
                await WorkflowNotifications.AddForUserAsync(db, assignedForReview, approved ? "Báo cáo tư vấn đã được duyệt" : "Báo cáo tư vấn cần chỉnh sửa",
                    $"Mở yêu cầu tư vấn #{id} để xem kết quả duyệt và link thanh toán nếu có.", lead.UpdatedAt!.Value, ct);
            }
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return result;
    }
}
