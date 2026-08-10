using System.Text.Json;
using EasyLotteryApplication.Payments;
using EasyLotteryDomain.Models.Overtime;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.Payments;

public abstract class SqliteJsonRepositoryBase(IOptions<StorageProviderOptions> options)
{
    protected readonly string ConnectionString = options.Value.ConnectionString!;
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    protected static T Deserialize<T>(string value) =>
        JsonSerializer.Deserialize<T>(value, JsonOptions) ?? throw new InvalidDataException("SQLite JSON payload 無法解析。");
}

public sealed class SqlitePaymentEventRepository(IOptions<StorageProviderOptions> options) :
    SqliteJsonRepositoryBase(options), IPaymentEventRepository, IPaymentEventProcessingRepository
{
    public async Task<IReadOnlyList<ProcessedPaymentEvent>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM payment_events ORDER BY rowid";
        var events = new List<ProcessedPaymentEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            events.Add(Deserialize<ProcessedPaymentEvent>(reader.GetString(0)));
        return events;
    }

    public async Task AppendAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO payment_events (provider_id, external_id, payload_json) VALUES ($provider, $external, $payload)";
        AddIdentity(command, paymentEvent);
        command.Parameters.AddWithValue("$payload", Serialize(paymentEvent));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PaymentEventClaimResult> TryClaimAsync(ProcessedPaymentEvent paymentEvent, TimeSpan processingLease, CancellationToken cancellationToken = default)
    {
        if (processingLease <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(processingLease), "付款事件處理租約必須大於零。");

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var existing = await FindAsync(connection, transaction, paymentEvent, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            paymentEvent.ProcessingState = "processing";
            paymentEvent.ProcessingClaimedAtUtc = now;
            paymentEvent.ProcessingCompletedAtUtc = null;
            paymentEvent.ProcessingFailureReason = "";
            paymentEvent.ProcessingAttempt = Math.Max(1, paymentEvent.ProcessingAttempt + 1);
            await InsertAsync(connection, transaction, paymentEvent, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentEventClaimResult(PaymentEventClaimStatus.Claimed, paymentEvent);
        }

        if (string.Equals(existing.ProcessingState, "completed", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentEventClaimResult(PaymentEventClaimStatus.AlreadyCompleted, existing);
        }

        if (string.Equals(existing.ProcessingState, "processing", StringComparison.OrdinalIgnoreCase)
            && existing.ProcessingClaimedAtUtc is { } claimedAt
            && now - claimedAt < processingLease)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentEventClaimResult(PaymentEventClaimStatus.InProgress, existing);
        }

        existing.ProcessingState = "processing";
        existing.ProcessingClaimedAtUtc = now;
        existing.ProcessingCompletedAtUtc = null;
        existing.ProcessingFailureReason = "";
        existing.ProcessingAttempt++;
        await UpdateRowAsync(connection, transaction, existing, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PaymentEventClaimResult(PaymentEventClaimStatus.Claimed, existing);
    }

    public async Task UpdateAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE payment_events SET payload_json = $payload WHERE provider_id = $provider AND external_id = $external";
        AddIdentity(command, paymentEvent);
        command.Parameters.AddWithValue("$payload", Serialize(paymentEvent));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException("找不到要更新的付款事件。");
    }

    private async Task<ProcessedPaymentEvent?> FindAsync(SqliteConnection connection, SqliteTransaction transaction, ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload_json FROM payment_events WHERE provider_id = $provider AND external_id = $external";
        AddIdentity(command, paymentEvent);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? Deserialize<ProcessedPaymentEvent>(json) : null;
    }

    private async Task InsertAsync(SqliteConnection connection, SqliteTransaction transaction, ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO payment_events (provider_id, external_id, payload_json) VALUES ($provider, $external, $payload)";
        AddIdentity(command, paymentEvent);
        command.Parameters.AddWithValue("$payload", Serialize(paymentEvent));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateRowAsync(SqliteConnection connection, SqliteTransaction transaction, ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE payment_events SET payload_json = $payload WHERE provider_id = $provider AND external_id = $external";
        AddIdentity(command, paymentEvent);
        command.Parameters.AddWithValue("$payload", Serialize(paymentEvent));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddIdentity(SqliteCommand command, ProcessedPaymentEvent paymentEvent)
    {
        command.Parameters.AddWithValue("$provider", paymentEvent.ProviderId);
        command.Parameters.AddWithValue("$external", paymentEvent.ExternalId);
    }
}

public sealed class SqlitePaymentOrderRepository(IOptions<StorageProviderOptions> options) :
    SqliteJsonRepositoryBase(options), IPaymentOrderRepository, IPaymentOrderMutationRepository
{
    public async Task<IReadOnlyList<RegisteredPaymentOrder>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM payment_orders ORDER BY rowid";
        var orders = new List<RegisteredPaymentOrder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            orders.Add(Deserialize<RegisteredPaymentOrder>(reader.GetString(0)));
        return orders;
    }

    public async Task SaveAsync(IReadOnlyList<RegisteredPaymentOrder> orders, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ReplaceAsync(connection, transaction, orders, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MutateAsync(Func<List<RegisteredPaymentOrder>, Task> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var orders = await ReadAsync(connection, transaction, cancellationToken);
        await mutation(orders);
        await ReplaceAsync(connection, transaction, orders, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<List<RegisteredPaymentOrder>> ReadAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload_json FROM payment_orders ORDER BY rowid";
        var orders = new List<RegisteredPaymentOrder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            orders.Add(Deserialize<RegisteredPaymentOrder>(reader.GetString(0)));
        return orders;
    }

    private async Task ReplaceAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<RegisteredPaymentOrder> orders, CancellationToken cancellationToken)
    {
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM payment_orders";
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var order in orders)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO payment_orders (provider_id, merchant_order_no, payload_json) VALUES ($provider, $merchant, $payload)";
            insert.Parameters.AddWithValue("$provider", order.ProviderId);
            insert.Parameters.AddWithValue("$merchant", order.MerchantOrderNo);
            insert.Parameters.AddWithValue("$payload", Serialize(order));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

public sealed class SqliteOvertimeFeedRepository(IOptions<StorageProviderOptions> options) :
    SqliteJsonRepositoryBase(options), IOvertimeFeedRepository, IOvertimeFeedMutationRepository
{
    public async Task<IReadOnlyList<OvertimeSupportEvent>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM overtime_feed ORDER BY occurred_at_utc DESC";
        var events = new List<OvertimeSupportEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            events.Add(Deserialize<OvertimeSupportEvent>(reader.GetString(0)));
        return events;
    }

    public async Task SaveAsync(IReadOnlyList<OvertimeSupportEvent> events, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ReplaceAsync(connection, transaction, events, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MutateAsync(Func<List<OvertimeSupportEvent>, Task> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var events = await ReadAsync(connection, transaction, cancellationToken);
        await mutation(events);
        await ReplaceAsync(connection, transaction, events, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<List<OvertimeSupportEvent>> ReadAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload_json FROM overtime_feed ORDER BY occurred_at_utc DESC";
        var events = new List<OvertimeSupportEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            events.Add(Deserialize<OvertimeSupportEvent>(reader.GetString(0)));
        return events;
    }

    private async Task ReplaceAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<OvertimeSupportEvent> events, CancellationToken cancellationToken)
    {
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM overtime_feed";
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var item in events)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO overtime_feed (id, occurred_at_utc, payload_json) VALUES ($id, $occurred, $payload)";
            insert.Parameters.AddWithValue("$id", item.Id.ToString("D"));
            insert.Parameters.AddWithValue("$occurred", item.OccurredAtUtc.ToString("O"));
            insert.Parameters.AddWithValue("$payload", Serialize(item));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
