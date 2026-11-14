using EasyLotteryCardHub.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace EasyLotteryCardHub.Health;

public sealed class CardHubDatabaseHealthCheck(HubDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (database.DataSource is null) return HealthCheckResult.Unhealthy("Card Hub database is not configured.");
        try
        {
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("Card Hub database is unavailable.", exception);
        }
    }
}
