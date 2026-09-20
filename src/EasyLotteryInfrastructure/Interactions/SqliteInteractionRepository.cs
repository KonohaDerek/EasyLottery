using System.Text.Json;
using EasyLotteryApplication.Interactions;
using EasyLotteryDomain.Models.Interactions;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.Interactions;

public sealed class SqliteInteractionRepository(IOptions<StorageProviderOptions> options) : IInteractionRepository
{
    private readonly string _connectionString = options.Value.ConnectionString!;

    public async Task<InteractionsYamlDocument> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM interaction_documents WHERE id = 1";
        var payload = await command.ExecuteScalarAsync(cancellationToken) as string;
        return payload is null
            ? new InteractionsYamlDocument()
            : JsonSerializer.Deserialize<InteractionsYamlDocument>(payload, JsonOptions)
                ?? throw new InvalidDataException("互動資料無法解析。");
    }

    public async Task<bool> ApplyAsync(
        InteractionEvent interactionEvent,
        InteractionDecision decision,
        AudienceProfile profile,
        PlatformIdentity identity,
        InteractionRound round,
        CancellationToken cancellationToken = default)
    {
        if (decision.AudienceProfileId != profile.Id || identity.AudienceProfileId != profile.Id || decision.RoundId != round.Id)
        {
            throw new InvalidOperationException("互動判定必須使用同一觀眾與回合。");
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using var claim = connection.CreateCommand();
        claim.Transaction = transaction;
        claim.CommandText = "INSERT OR IGNORE INTO interaction_event_keys (platform, channel_scope, external_event_id) VALUES ($platform, $scope, $eventId)";
        claim.Parameters.AddWithValue("$platform", interactionEvent.Platform.ToString());
        claim.Parameters.AddWithValue("$scope", interactionEvent.ChannelScope);
        claim.Parameters.AddWithValue("$eventId", interactionEvent.ExternalEventId);
        if (await claim.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var document = await ReadUnsafeAsync(connection, transaction, cancellationToken);
        Upsert(document.AudienceProfiles, profile, item => item.Id);
        Upsert(document.PlatformIdentities, identity, item => item.Id);
        Upsert(document.Rounds, round, item => item.Id);
        document.ProcessedEventKeys.Add(interactionEvent.EventKey);

        await using var save = connection.CreateCommand();
        save.Transaction = transaction;
        save.CommandText = "INSERT INTO interaction_documents (id, payload_json, updated_at_utc) VALUES (1, $payload, CURRENT_TIMESTAMP) ON CONFLICT(id) DO UPDATE SET payload_json = excluded.payload_json, updated_at_utc = excluded.updated_at_utc";
        save.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(document, JsonOptions));
        await save.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<InteractionsYamlDocument> ReadUnsafeAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload_json FROM interaction_documents WHERE id = 1";
        var payload = await command.ExecuteScalarAsync(cancellationToken) as string;
        return payload is null
            ? new InteractionsYamlDocument()
            : JsonSerializer.Deserialize<InteractionsYamlDocument>(payload, JsonOptions)
                ?? throw new InvalidDataException("互動資料無法解析。");
    }

    private static void Upsert<T>(List<T> items, T item, Func<T, Guid> id) where T : class
    {
        var index = items.FindIndex(existing => id(existing) == id(item));
        if (index < 0) items.Add(item);
        else items[index] = item;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
