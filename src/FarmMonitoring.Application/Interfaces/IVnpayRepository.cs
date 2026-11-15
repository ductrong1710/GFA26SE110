using FarmMonitoring.Domain.Entities;
namespace FarmMonitoring.Application.Interfaces;

public interface IVnpayRepository
{
    Task<T> ApplyAsync<T>(string txnRef, Func<Consultation,List<SiteVisitProposal>,VnpayTransaction,T> action, Func<T> notFound, CancellationToken ct);
    Task<string?> PublicStatusAsync(string token, DateTimeOffset now, CancellationToken ct);
}
