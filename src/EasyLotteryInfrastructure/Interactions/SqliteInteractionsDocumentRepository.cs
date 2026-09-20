using System.Text.Json;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Interactions;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.Interactions;

public sealed class SqliteInteractionsDocumentRepository(IOptions<StorageProviderOptions> options) : IInteractionsYamlDocumentRepository
{
    private readonly string _connectionString = options.Value.ConnectionString!;

    public async Task<InteractionsYamlDocument> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM interaction_documents WHERE id = 1";
        var payload = await command.ExecuteScalarAsync(cancellationToken) as string;
        return payload is null ? new InteractionsYamlDocument() : JsonSerializer.Deserialize<InteractionsYamlDocument>(payload, JsonOptions)
            ?? throw new InvalidDataException("互動資料無法解析。");
    }

    public async Task SaveAsync(InteractionsYamlDocument document, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO interaction_documents (id, payload_json, updated_at_utc) VALUES (1, $payload, CURRENT_TIMESTAMP) ON CONFLICT(id) DO UPDATE SET payload_json = excluded.payload_json, updated_at_utc = excluded.updated_at_utc";
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(document, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
