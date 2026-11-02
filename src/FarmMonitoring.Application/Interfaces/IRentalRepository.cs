using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Rentals;

namespace FarmMonitoring.Application.Interfaces;
public interface IRentalRepository
{
    Task<PagedResult<RentalSummary>> ListAsync(int actorId, PageQuery query, DateTimeOffset now, CancellationToken ct);
    Task<RentalDetail> GetAsync(int id, int actorId, DateTimeOffset now, CancellationToken ct);
    Task<int> CreateAsync(int jobId, int actorId, CreateRentalRequest request, DateTimeOffset now, CancellationToken ct);
    Task ChangeAsync(int id, int actorId, string action, int expectedRevision, string note, DateTimeOffset? endAt, DateTimeOffset now, CancellationToken ct);
    Task MonitorAsync(DateTimeOffset now, int warningDays, int batchSize, CancellationToken ct);
}
