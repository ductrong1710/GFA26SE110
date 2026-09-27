using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IEmailDeliveryRepository
{
    Task<bool> ProcessNextAsync(Func<Notification, Task> deliver, CancellationToken ct);
}
