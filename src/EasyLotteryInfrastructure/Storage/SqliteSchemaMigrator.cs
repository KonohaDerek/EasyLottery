using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.Storage;

public sealed class SqliteSchemaMigrator(
    IOptions<StorageProviderOptions> options,
    ILogger<SqliteSchemaMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var storage = options.Value;
        if (storage.NormalizedProvider != "sqlite") return;

        await using var connection = new SqliteConnection(storage.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                applied_at_utc TEXT NOT NULL
            );
            INSERT OR IGNORE INTO schema_migrations (version, applied_at_utc)
            VALUES (1, CURRENT_TIMESTAMP);
            INSERT OR IGNORE INTO schema_migrations (version, applied_at_utc)
            VALUES (2, CURRENT_TIMESTAMP);
            CREATE TABLE IF NOT EXISTS config_documents (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                payload_json TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS donate_activities (
                id INTEGER PRIMARY KEY,
                public_id TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                type INTEGER NOT NULL,
                minimum_donation_amount REAL NOT NULL,
                starts_at_utc TEXT NOT NULL,
                ends_at_utc TEXT NOT NULL,
                animation INTEGER NOT NULL,
                is_enabled INTEGER NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS donate_activity_events (
                sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                activity_id INTEGER NOT NULL,
                event_type TEXT NOT NULL,
                occurred_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS poke_templates (
                id INTEGER PRIMARY KEY,
                public_id TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS roulette_templates (
                id INTEGER PRIMARY KEY,
                public_id TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS activity_results (
                id INTEGER PRIMARY KEY,
                activity_type TEXT NOT NULL,
                source_id INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS payment_events (
                provider_id TEXT NOT NULL,
                external_id TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                PRIMARY KEY (provider_id, external_id)
            );
            CREATE TABLE IF NOT EXISTS payment_orders (
                provider_id TEXT NOT NULL,
                merchant_order_no TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                PRIMARY KEY (provider_id, merchant_order_no)
            );
            CREATE TABLE IF NOT EXISTS overtime_feed (
                id TEXT PRIMARY KEY,
                occurred_at_utc TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS obs_assets (
                id TEXT PRIMARY KEY,
                payload_json TEXT NOT NULL,
                content BLOB NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("SQLite storage schema is ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
