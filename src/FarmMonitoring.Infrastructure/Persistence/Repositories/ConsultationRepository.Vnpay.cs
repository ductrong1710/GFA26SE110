using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed partial class ConsultationRepository : IVnpayRepository
{
    public async Task<T> ApplyAsync<T>(string txnRef, Func<Consultation,List<SiteVisitProposal>,VnpayTransaction,T> action, Func<T> notFound, CancellationToken ct)
    {
        var id = await (from t in db.Set<VnpayTransaction>() join p in db.Set<SiteVisitProposal>() on t.ProposalId equals p.Id
                        where t.TxnRef == txnRef select (int?)p.ConsultationId).SingleOrDefaultAsync(ct);
        if (id is null) return notFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Same lock as manual receipts, checkout, revisions and scheduling.
        var lead = await LockAsync(id.Value, ct);
        var revisions = await db.Set<SiteVisitProposal>().Include(x => x.Payment).Include(x => x.Transactions)
            .Where(x => x.ConsultationId == id).OrderBy(x => x.Revision).ToListAsync(ct);
        var attempt = revisions.SelectMany(x => x.Transactions).Single(x => x.TxnRef == txnRef);
        var result = action(lead,revisions,attempt);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<string?> PublicStatusAsync(string token, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.Set<VnpayTransaction>().AsNoTracking().Where(x => x.PublicToken == token)
            .Select(x => new {x.Status,x.ExpiresAt}).SingleOrDefaultAsync(ct);
        return row is null ? null : row.Status == "PENDING" && row.ExpiresAt <= now ? "UNKNOWN" : row.Status;
    }
}
