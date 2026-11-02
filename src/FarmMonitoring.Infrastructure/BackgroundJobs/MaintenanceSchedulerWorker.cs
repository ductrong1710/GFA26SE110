using FarmMonitoring.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FarmMonitoring.Infrastructure.BackgroundJobs;
public sealed class MaintenanceSchedulerSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
}
public sealed class MaintenanceSchedulerWorker(IServiceScopeFactory scopes, MaintenanceSchedulerSettings settings,
    TimeProvider clock, ILogger<MaintenanceSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var now = clock.GetUtcNow(); var afterId = 0;
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        int[] ids;
                        await using (var scope = scopes.CreateAsyncScope())
                            ids = await scope.ServiceProvider.GetRequiredService<IMaintenanceScheduleRepository>().DueIdsAsync(afterId, now, stoppingToken);
                        if (ids.Length == 0) break;
                        foreach (var id in ids)
                        {
                            try
                            {
                                await using var scope = scopes.CreateAsyncScope();
                                await scope.ServiceProvider.GetRequiredService<IMaintenanceScheduleRepository>().ProcessDueAsync(id, now, stoppingToken);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                            catch (Exception ex) { logger.LogError(ex, "Maintenance schedule {ScheduleId} failed; retrying next interval.", id); }
                        }
                        afterId = ids[^1];
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Maintenance scheduler scan failed; retrying next interval."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
