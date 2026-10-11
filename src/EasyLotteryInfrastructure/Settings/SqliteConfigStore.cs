using System.Text.Json;
using System.Runtime.CompilerServices;
using EasyLotteryInfrastructure.DonateActivities;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.Settings;

public sealed class SqliteConfigStore(
    IOptions<StorageProviderOptions> options,
    SqliteDonateActivityRepository? donateRepository = null) : IEasyLotteryConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConditionalWeakTable<EasyLotteryConfigDocument, SettingsBaseline> _loadedDocuments = new();

    public async Task<EasyLotteryConfigDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM config_documents WHERE id = 1";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        var document = value is string json
            ? JsonSerializer.Deserialize<EasyLotteryConfigDocument>(json, JsonOptions) ?? new EasyLotteryConfigDocument()
            : new EasyLotteryConfigDocument();
        _loadedDocuments.Add(document, new SettingsBaseline(SerializeDrawingRules(document)));
        return document;
    }

    public async Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        _loadedDocuments.TryGetValue(document, out var baseline);
        if (baseline is not null && baseline.DrawingRulesJson == SerializeDrawingRules(document))
        {
            // Legacy load-edit-save callers do not own DrawingRules. Preserve a
            // newer resource update instead of writing their stale full snapshot.
            var current = await ReadDocumentAsync(connection, transaction, cancellationToken);
            document.DrawingRules = current.DrawingRules;
        }
        await WriteDocumentAsync(connection, transaction, document, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (baseline is not null) baseline.DrawingRulesJson = SerializeDrawingRules(document);
        if (donateRepository is not null)
            await donateRepository.SaveSnapshotAsync(document.DonateLotteryActivities, cancellationToken);
    }

    public Task<TResult> MutateSettingsAsync<TResult>(Func<EasyLotteryConfigDocument, TResult> update, CancellationToken cancellationToken = default) =>
        MutateSettingsAsync(update, updateDonateSnapshot: false, cancellationToken);

    public async Task<TResult> MutateSettingsAsync<TResult>(
        Func<EasyLotteryConfigDocument, TResult> update,
        bool updateDonateSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var document = await ReadDocumentAsync(connection, transaction, cancellationToken);
        var result = update(document);
        await WriteDocumentAsync(connection, transaction, document, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (updateDonateSnapshot && donateRepository is not null)
            await donateRepository.SaveSnapshotAsync(document.DonateLotteryActivities, cancellationToken);
        return result;
    }

    private static async Task<EasyLotteryConfigDocument> ReadDocumentAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload_json FROM config_documents WHERE id = 1";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json
            ? JsonSerializer.Deserialize<EasyLotteryConfigDocument>(json, JsonOptions) ?? new EasyLotteryConfigDocument()
            : new EasyLotteryConfigDocument();
    }

    private static async Task WriteDocumentAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        EasyLotteryConfigDocument document,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO config_documents (id, payload_json, updated_at_utc) VALUES (1, $payload, $updated) ON CONFLICT(id) DO UPDATE SET payload_json = excluded.payload_json, updated_at_utc = excluded.updated_at_utc";
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(document, JsonOptions));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SerializeDrawingRules(EasyLotteryConfigDocument document) =>
        JsonSerializer.Serialize(document.DrawingRules, JsonOptions);

    private sealed class SettingsBaseline(string drawingRulesJson)
    {
        public string DrawingRulesJson { get; set; } = drawingRulesJson;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
