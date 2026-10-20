using System.Threading.Channels;
using FarmMonitoring.Application.Features.Auth;
using FarmMonitoring.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FarmMonitoring.Infrastructure.BackgroundJobs;

public sealed class PasswordResetQueue(ILogger<PasswordResetQueue> logger) : IPasswordResetQueue
{
    // Queue only addresses, never reset tokens. Bounded memory; overload has a generic public response.
    private readonly Channel<string> requests = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    public void Enqueue(string normalizedEmail)
    {
        if (!requests.Writer.TryWrite(normalizedEmail)) logger.LogWarning("Password recovery queue is full; request was not queued.");
    }
    public IAsyncEnumerable<string> ReadAsync(CancellationToken ct) => requests.Reader.ReadAllAsync(ct);
}

public sealed class PasswordResetWorker(PasswordResetQueue queue, IServiceScopeFactory scopes,
    ILogger<PasswordResetWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in queue.ReadAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>().DeliverResetAsync(email, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Transport exceptions can contain addresses or message contents; never log them.
                logger.LogWarning("Password recovery delivery failed. Check SMTP configuration and availability.");
            }
        }
    }
}
