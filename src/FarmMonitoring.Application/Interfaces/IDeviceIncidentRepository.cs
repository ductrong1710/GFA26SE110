using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;
public interface IDeviceIncidentRepository
{
    Task<PagedResult<DeviceIncidentSummary>> ListAsync(int actorId, PageQuery query, CancellationToken ct);
    Task<DeviceIncidentDetail> GetAsync(int id, int actorId, CancellationToken ct);
    Task<int> CreateAsync(int actorId, CreateDeviceIncidentRequest request, List<EvidenceImage> images, DateTimeOffset now, CancellationToken ct);
}
