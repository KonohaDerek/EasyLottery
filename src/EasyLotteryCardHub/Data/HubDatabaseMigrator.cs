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
    private const string MigrationResource = "EasyLotteryCardHub.Data.001_hub_foundation.sql";

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

        await using (var check = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM card_hub_schema_migrations WHERE version = 1)", connection, transaction))
        {
            if ((bool)(await check.ExecuteScalarAsync(cancellationToken) ?? false))
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }
        }

        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(MigrationResource)
            ?? throw new InvalidOperationException($"Embedded migration '{MigrationResource}' was not found.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(cancellationToken);
        await using (var migration = new NpgsqlCommand(sql, connection, transaction))
            await migration.ExecuteNonQueryAsync(cancellationToken);
        await using (var record = new NpgsqlCommand("INSERT INTO card_hub_schema_migrations (version) VALUES (1)", connection, transaction))
            await record.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Applied Card Hub database migration {Version}.", 1);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
