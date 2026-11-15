using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public enum VisitAccess { Reader, Engineer, Administrator }

public interface ISiteVisitRepository
{
    Task<IReadOnlyList<SiteVisitOffice>> ListOfficesAsync(CancellationToken ct);
    Task<SiteVisitOffice> AddOfficeAsync(int adminId, string name, string mapsUrl, CancellationToken ct);

    // Mutations execute while holding the actor and consultation row locks.
    Task<T> WithVisitAsync<T>(int id, int actor, VisitAccess access, bool write,
        Func<Consultation, List<SiteVisitProposal>, bool, T> action, CancellationToken ct);
}
