using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using FarmMonitoring.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.UnitTests;
public sealed class MaintenanceWorkerTests
{
    [Theory]
    [InlineData("0", false)]
    [InlineData("86401", false)]
    [InlineData("1", true)]
    [InlineData("86400", true)]
    public void Configured_scheduler_rejects_invalid_intervals(string interval, bool valid)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MaintenanceScheduler:IntervalSeconds"] = interval,
            ["MaintenanceScheduler:Enabled"] = "false",
        }).Build();
        using var services = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
        if (!valid) Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<MaintenanceSchedulerSettings>());
        else
        {
            var settings = services.GetRequiredService<MaintenanceSchedulerSettings>();
            Assert.Equal(int.Parse(interval), settings.IntervalSeconds);
            Assert.False(settings.Enabled);
        }
    }
    [Fact]
    public async Task Worker_continues_after_failed_schedule_and_scans_later_pages()
    {
        var repository = new ScanRepository();
        await using var services = new ServiceCollection().AddScoped<IMaintenanceScheduleRepository>(_ => repository).BuildServiceProvider();
        using var worker = new MaintenanceSchedulerWorker(services.GetRequiredService<IServiceScopeFactory>(),
            new() { IntervalSeconds = 1 }, TimeProvider.System, NullLogger<MaintenanceSchedulerWorker>.Instance);
        await worker.StartAsync(default);
        try
        {
            await repository.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { await worker.StopAsync(default); }
        Assert.Equal(new[] { 1, 2, 3 }, repository.Processed);
        Assert.Equal(new[] { 0, 2 }, repository.Cursors.Take(2));
    }

    [Fact]
    public async Task Disabled_worker_completes_without_scanning()
    {
        var repository = new ScanRepository();
        await using var services = new ServiceCollection().AddScoped<IMaintenanceScheduleRepository>(_ => repository).BuildServiceProvider();
        using var worker = new MaintenanceSchedulerWorker(services.GetRequiredService<IServiceScopeFactory>(),
            new() { Enabled = false }, TimeProvider.System, NullLogger<MaintenanceSchedulerWorker>.Instance);
        await worker.StartAsync(default);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(default);
        Assert.Empty(repository.Cursors); Assert.Empty(repository.Processed);
    }

    private sealed class ScanRepository : IMaintenanceScheduleRepository
    {
        public Task<PagedResult<MaintenanceOccurrenceResponse>> OccurrencesAsync(int id, int actorId, PageQuery query, CancellationToken ct) => throw new NotSupportedException();
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> Cursors { get; } = [];
        public List<int> Processed { get; } = [];
        public Task<int[]> DueIdsAsync(int afterId, DateTimeOffset now, CancellationToken ct)
        {
            Cursors.Add(afterId);
            return Task.FromResult(afterId switch { 0 => new[] { 1, 2 }, 2 => new[] { 3 }, _ => [] });
        }
        public Task<int> ProcessDueAsync(int id, DateTimeOffset now, CancellationToken ct)
        {
            Processed.Add(id);
            if (id == 1) throw new InvalidOperationException("Simulated database failure");
            if (id == 3) Finished.TrySetResult();
            return Task.FromResult(1);
        }
        public Task<PagedResult<MaintenanceScheduleResponse>> ListAsync(int actorId, PageQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<MaintenanceScheduleResponse> GetAsync(int id, int actorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<MaintenanceScheduleResponse> SaveAsync(int? id, int actorId, MaintenanceScheduleRequest request, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<MaintenanceScheduleResponse> DisableAsync(int id, int actorId, int expectedRevision, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    }
}
