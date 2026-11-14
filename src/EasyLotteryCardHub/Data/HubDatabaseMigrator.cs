using System.Reflection;
using Npgsql;

namespace EasyLotteryCardHub.Data;

public sealed class HubDatabase(NpgsqlDataSource? dataSource) : IDisposable
{
    public NpgsqlDataSource? DataSource { get; } = dataSource;
    public void Dispose() => DataSource?.Dispose();
}

public sealed class HubDatabaseMigrator(HubDatabase database, ILogger<HubDatabaseMigrator> logger) : IHostedService
{
    private static readonly (int Version, string Resource)[] Migrations =
    [
        (1, "EasyLotteryCardHub.Data.001_hub_foundation.sql"),
        (2, "EasyLotteryCardHub.Data.002_viewer_accounts.sql")
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (database.DataSource is null)
        {
            logger.LogWarning("Card Hub database is not configured; readiness will remain unhealthy.");
            return;
        }

        var dataSource = database.DataSource;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var migrationLock = new NpgsqlCommand("SELECT pg_advisory_xact_lock(712438921, 1)", connection, transaction))
            await migrationLock.ExecuteNonQueryAsync(cancellationToken);
        await using (var command = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS card_hub_schema_migrations (version integer PRIMARY KEY, applied_at_utc timestamptz NOT NULL DEFAULT now())", connection, transaction))
            await command.ExecuteNonQueryAsync(cancellationToken);

        foreach (var (version, resource) in Migrations)
        {
            await using var check = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM card_hub_schema_migrations WHERE version = @version)", connection, transaction);
            check.Parameters.AddWithValue("version", version);
            if ((bool)(await check.ExecuteScalarAsync(cancellationToken) ?? false)) continue;

            await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded migration '{resource}' was not found.");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);
            await using (var migration = new NpgsqlCommand(sql, connection, transaction))
                await migration.ExecuteNonQueryAsync(cancellationToken);
            await using (var record = new NpgsqlCommand("INSERT INTO card_hub_schema_migrations (version) VALUES (@version)", connection, transaction))
            {
                record.Parameters.AddWithValue("version", version);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
            logger.LogInformation("Applied Card Hub database migration {Version}.", version);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
