using Npgsql;

namespace FarmMonitoring.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";

    public bool IsValid()
    {
        try
        {
            var value = new NpgsqlConnectionStringBuilder(ConnectionString);
            return !string.IsNullOrWhiteSpace(value.Host) && !string.IsNullOrWhiteSpace(value.Database);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
