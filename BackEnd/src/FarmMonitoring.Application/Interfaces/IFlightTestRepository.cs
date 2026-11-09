using FarmMonitoring.Domain.Entities;
namespace FarmMonitoring.Application.Interfaces;
public interface IFlightTestRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task<FlightTest?> FindAsync(int id, CancellationToken ct);
    Task<FlightTest?> FindRequestAsync(int gatewayId, string key, CancellationToken ct);
    Task<FlightTest?> NextAsync(int gatewayId, DateTimeOffset now, CancellationToken ct);
    Task<Gateway?> GatewayAsync(int id, CancellationToken ct);
    Task<Uav?> UavAsync(int id, CancellationToken ct);
    Task<bool> HasConflictAsync(int gatewayId, int uavId, int excludingId, DateTimeOffset now, CancellationToken ct);
    void Add(FlightTest test);
    Task SaveAsync(CancellationToken ct);
}
