using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace EasyLotteryInfrastructure.Shop;

/// <summary>Stores the encrypted Card Hub site credential in the SQLite shop schema.</summary>
public sealed class SqliteHubLinkRepository(StorageProviderOptions options)
{
    public async Task<HubLinkRecord?> ReadAsync(CancellationToken cancellationToken = default)
    {
        EnsureSqlite();
        await using var connection = new SqliteConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT hub_base_url, site_client_id, encrypted_secret, tenant_id, last_publish_at
            FROM hub_link
            WHERE id = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new HubLinkRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    public async Task SaveAsync(HubLinkRecord record, CancellationToken cancellationToken = default)
    {
        EnsureSqlite();
        await using var connection = new SqliteConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO hub_link (id, hub_base_url, site_client_id, encrypted_secret, tenant_id, last_publish_at)
            VALUES (1, $hubBaseUrl, $siteClientId, $encryptedSecret, $tenantId, $lastPublishAt)
            ON CONFLICT(id) DO UPDATE SET
                hub_base_url = excluded.hub_base_url,
                site_client_id = excluded.site_client_id,
                encrypted_secret = excluded.encrypted_secret,
                tenant_id = excluded.tenant_id,
                last_publish_at = excluded.last_publish_at;
            """;
        command.Parameters.AddWithValue("$hubBaseUrl", record.HubBaseUrl);
        command.Parameters.AddWithValue("$siteClientId", record.SiteClientId);
        command.Parameters.AddWithValue("$encryptedSecret", record.EncryptedSecret);
        command.Parameters.AddWithValue("$tenantId", (object?)record.TenantId ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastPublishAt", (object?)record.LastPublishAt ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void EnsureSqlite()
    {
        if (options.NormalizedProvider != "sqlite")
            throw new InvalidOperationException("卡牌商店需要使用 SQLite 儲存。");
    }
}
