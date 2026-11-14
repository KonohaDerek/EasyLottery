using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EasyLotteryApi.CardShop;

internal sealed record CardHubLinkRecord(
    string HubUrl,
    string SiteClientId,
    string EncryptedSecret,
    string? TenantId,
    string? TenantName,
    string ConnectionState,
    DateTimeOffset? LastCheckedAtUtc,
    DateTimeOffset? LastPublishAtUtc);

internal interface ICardHubLinkRepository
{
    Task<CardHubLinkRecord?> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(CardHubLinkRecord record, CancellationToken cancellationToken);
    Task DeleteAsync(CancellationToken cancellationToken);
}

internal sealed class SqliteCardHubLinkRepository(
    IConfiguration configuration,
    IHostEnvironment environment,
    IOptions<StorageProviderOptions> storage) : ICardHubLinkRepository
{
    private readonly string _connectionString = CardShopSqliteConnection.ResolveConnectionString(configuration, environment, storage.Value);

    public async Task<CardHubLinkRecord?> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT hub_url, site_client_id, encrypted_secret, tenant_id, tenant_name,
                   connection_state, last_checked_at_utc, last_publish_at_utc
            FROM hub_link WHERE id = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new CardHubLinkRecord(
            reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5), ReadDate(reader, 6), ReadDate(reader, 7));
    }

    public async Task SaveAsync(CardHubLinkRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO hub_link (id, hub_url, site_client_id, encrypted_secret, tenant_id, tenant_name,
                                  connection_state, last_checked_at_utc, last_publish_at_utc)
            VALUES (1, $hubUrl, $clientId, $secret, $tenantId, $tenantName, $state, $checkedAt, $publishedAt)
            ON CONFLICT(id) DO UPDATE SET
                hub_url = excluded.hub_url,
                site_client_id = excluded.site_client_id,
                encrypted_secret = excluded.encrypted_secret,
                tenant_id = excluded.tenant_id,
                tenant_name = excluded.tenant_name,
                connection_state = excluded.connection_state,
                last_checked_at_utc = excluded.last_checked_at_utc,
                last_publish_at_utc = excluded.last_publish_at_utc;
            """;
        command.Parameters.AddWithValue("$hubUrl", record.HubUrl);
        command.Parameters.AddWithValue("$clientId", record.SiteClientId);
        command.Parameters.AddWithValue("$secret", record.EncryptedSecret);
        command.Parameters.AddWithValue("$tenantId", (object?)record.TenantId ?? DBNull.Value);
        command.Parameters.AddWithValue("$tenantName", (object?)record.TenantName ?? DBNull.Value);
        command.Parameters.AddWithValue("$state", record.ConnectionState);
        command.Parameters.AddWithValue("$checkedAt", (object?)record.LastCheckedAtUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$publishedAt", (object?)record.LastPublishAtUtc?.ToString("O") ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM hub_link WHERE id = 1;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static DateTimeOffset? ReadDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(reader.GetString(ordinal), System.Globalization.CultureInfo.InvariantCulture);
}
