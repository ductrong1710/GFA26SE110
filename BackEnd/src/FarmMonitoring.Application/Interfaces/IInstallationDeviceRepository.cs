using FarmMonitoring.Application.Features.Installations;
namespace FarmMonitoring.Application.Interfaces;
public interface IInstallationDeviceRepository
{
    Task<IReadOnlyList<AllocatedDeviceResponse>> GetAsync(int jobId, CancellationToken ct);
    Task SetAsync(int jobId, int actorId, JobDevicesRequest request, DateTimeOffset now, CancellationToken ct);
}
