using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public enum VisitAccess { Reader, Engineer, Administrator }

public interface ISiteVisitRepository
{
    // Mutations execute while holding the actor and consultation row locks.
    Task<T> WithVisitAsync<T>(int id, int actor, VisitAccess access, bool write,
        Func<Consultation, List<SiteVisitProposal>, bool, T> action, CancellationToken ct);
}
