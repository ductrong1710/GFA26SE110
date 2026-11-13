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

        var revisions = await db.Set<SiteVisitProposal>().Include(x => x.Payment).Where(x => x.ConsultationId == id)
            .OrderBy(x => x.Revision).ToListAsync(ct);
        if (admin && revisions.LastOrDefault()?.Payment is { } payment)
            await db.Entry(payment).Reference(x => x.Bill).LoadAsync(ct);
        var count = revisions.Count;
        var result = action(lead, revisions, admin);
        if (write)
        {
            if (revisions.Count > count) db.Add(revisions[^1]);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return result;
    }
}
