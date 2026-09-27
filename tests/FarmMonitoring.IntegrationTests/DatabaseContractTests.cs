using System.Text.RegularExpressions;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class DatabaseContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Migrated_database_has_all_baseline_tables_and_required_index_prefixes()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tables = await db.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'").ToArrayAsync();
        foreach (var table in new[] { "users", "roles", "user_roles", "refresh_tokens", "farms", "zones", "sensor_nodes", "sensor_types",
            "sensor_channels", "sensor_thresholds", "sensor_readings", "uavs", "gateways", "missions", "mission_waypoints", "mission_targets",
            "collection_attempts", "mission_logs", "telemetry_records", "sync_batches", "alerts", "alert_histories", "notifications" })
            Assert.Contains(table, tables);
        var definitions = await db.Database.SqlQueryRaw<string>("SELECT tablename || '|' || indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToArrayAsync();
        var indexes = definitions.Select(x => (Table: x.Split('|')[0], Columns: Regex.Match(x, @"\(([^)]+)\)").Groups[1].Value.Replace("\"", "").Replace(" ", ""))).ToArray();
        foreach (var (table, prefix) in new[]
        {
            ("users", "email"), ("sensor_nodes", "device_code"), ("sensor_nodes", "zone_id"),
            ("sensor_channels", "sensor_node_id"), ("sensor_channels", "sensor_type_id"),
            ("sensor_readings", "sensor_channel_id,measured_at"), ("sensor_readings", "mission_id"), ("sensor_readings", "gateway_id"),
            ("missions", "farm_id"), ("missions", "status"), ("missions", "scheduled_start_at"),
            ("mission_waypoints", "mission_id,sequence_no"), ("mission_targets", "mission_id"),
            ("telemetry_records", "mission_id,recorded_at"), ("sync_batches", "gateway_id,batch_key"),
            ("alerts", "status"), ("alerts", "alert_type"), ("alerts", "sensor_node_id"), ("alerts", "gateway_id"), ("alerts", "mission_id"),
            ("notifications", "user_id,status")
        })
            Assert.True(indexes.Any(x => x.Table == table && (x.Columns == prefix || x.Columns.StartsWith(prefix + ",", StringComparison.Ordinal))),
                $"Missing required index prefix: {table}({prefix}).");
    }
}
