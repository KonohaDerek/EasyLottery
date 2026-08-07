using System.Text.Json;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.Settings;

public sealed class SqliteConfigStore(IOptions<StorageProviderOptions> options) : IEasyLotteryConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EasyLotteryConfigDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM config_documents WHERE id = 1";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json
            ? JsonSerializer.Deserialize<EasyLotteryConfigDocument>(json, JsonOptions) ?? new EasyLotteryConfigDocument()
            : new EasyLotteryConfigDocument();
    }

    public async Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO config_documents (id, payload_json, updated_at_utc) VALUES (1, $payload, $updated) ON CONFLICT(id) DO UPDATE SET payload_json = excluded.payload_json, updated_at_utc = excluded.updated_at_utc";
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(document, JsonOptions));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
