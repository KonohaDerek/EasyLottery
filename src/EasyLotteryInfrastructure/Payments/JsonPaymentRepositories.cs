using System.Text.Json;
using EasyLotteryApplication.Payments;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryInfrastructure.Payments;

public sealed class JsonPaymentEventRepository : IPaymentEventRepository, IPaymentEventProcessingRepository
{
    private readonly IStorageGateProvider _storageGates;
    public string StoragePath { get; }

    public JsonPaymentEventRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
    {
        _storageGates = storageGates;
        var storageDirectory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(storageDirectory);
        StoragePath = System.IO.Path.Combine(storageDirectory, "easy-lottery-payment-events.json");
    }

    public async Task<IReadOnlyList<ProcessedPaymentEvent>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        return await ReadUnsafeAsync(cancellationToken);
    }

    public async Task AppendAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        var items = (await ReadUnsafeAsync(cancellationToken)).ToList();
        items.Add(paymentEvent);
        await WriteUnsafeAsync(items, cancellationToken);
    }

    public async Task<PaymentEventClaimResult> TryClaimAsync(
        ProcessedPaymentEvent paymentEvent,
        TimeSpan processingLease,
        CancellationToken cancellationToken = default)
    {
        if (processingLease <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(processingLease), "付款事件處理租約必須大於零。");
        }

        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        var items = (await ReadUnsafeAsync(cancellationToken)).ToList();
        var existing = items.FirstOrDefault(item => HasSameIdentity(item, paymentEvent));
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            paymentEvent.ProcessingState = "processing";
            paymentEvent.ProcessingClaimedAtUtc = now;
            paymentEvent.ProcessingCompletedAtUtc = null;
            paymentEvent.ProcessingFailureReason = "";
            paymentEvent.ProcessingAttempt = Math.Max(1, paymentEvent.ProcessingAttempt + 1);
            items.Add(paymentEvent);
            await WriteUnsafeAsync(items, cancellationToken);
            return new PaymentEventClaimResult(PaymentEventClaimStatus.Claimed, paymentEvent);
        }

        // Events written by older versions have no workflow state and are already
        // complete because the old processor appended only after validation.
        if (string.Equals(existing.ProcessingState, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentEventClaimResult(PaymentEventClaimStatus.AlreadyCompleted, existing);
        }

        if (string.Equals(existing.ProcessingState, "processing", StringComparison.OrdinalIgnoreCase)
            && existing.ProcessingClaimedAtUtc is { } claimedAt
            && now - claimedAt < processingLease)
        {
            return new PaymentEventClaimResult(PaymentEventClaimStatus.InProgress, existing);
        }

        // A failed or expired claim can be resumed. The progress flags remain so
        // completed side effects are not repeated during recovery.
        existing.ProcessingState = "processing";
        existing.ProcessingClaimedAtUtc = now;
        existing.ProcessingCompletedAtUtc = null;
        existing.ProcessingFailureReason = "";
        existing.ProcessingAttempt++;
        await WriteUnsafeAsync(items, cancellationToken);
        return new PaymentEventClaimResult(PaymentEventClaimStatus.Claimed, existing);
    }

    public async Task UpdateAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        var items = (await ReadUnsafeAsync(cancellationToken)).ToList();
        var index = items.FindIndex(item => HasSameIdentity(item, paymentEvent));
        if (index < 0)
        {
            throw new InvalidOperationException("找不到要更新的付款事件。");
        }

        items[index] = paymentEvent;
        await WriteUnsafeAsync(items, cancellationToken);
    }

    internal async Task<IReadOnlyList<ProcessedPaymentEvent>> ReadUnsafeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(StoragePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(StoragePath);
        return (await JsonSerializer.DeserializeAsync<List<ProcessedPaymentEvent>>(stream, cancellationToken: cancellationToken)) ?? [];
    }

    internal async Task WriteUnsafeAsync(IReadOnlyList<ProcessedPaymentEvent> items, CancellationToken cancellationToken = default)
    {
        var temporaryPath = $"{StoragePath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, items, cancellationToken: cancellationToken);
        }
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }

    private static bool HasSameIdentity(ProcessedPaymentEvent left, ProcessedPaymentEvent right) =>
        string.Equals(left.ProviderId, right.ProviderId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.ExternalId, right.ExternalId, StringComparison.Ordinal);
}

public sealed class JsonPaymentOrderRepository : IPaymentOrderRepository, IPaymentOrderMutationRepository
{
    private readonly IStorageGateProvider _storageGates;
    public string StoragePath { get; }

    public JsonPaymentOrderRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
    {
        _storageGates = storageGates;
        var storageDirectory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(storageDirectory);
        StoragePath = System.IO.Path.Combine(storageDirectory, "easy-lottery-payment-orders.json");
    }

    public async Task<IReadOnlyList<RegisteredPaymentOrder>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        return await ReadUnsafeAsync(cancellationToken);
    }

    public async Task SaveAsync(IReadOnlyList<RegisteredPaymentOrder> orders, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        await WriteUnsafeAsync(orders, cancellationToken);
    }

    public async Task MutateAsync(Func<List<RegisteredPaymentOrder>, Task> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        var orders = (await ReadUnsafeAsync(cancellationToken)).ToList();
        await mutation(orders);
        await WriteUnsafeAsync(orders, cancellationToken);
    }

    internal async Task<IReadOnlyList<RegisteredPaymentOrder>> ReadUnsafeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(StoragePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(StoragePath);
        return (await JsonSerializer.DeserializeAsync<List<RegisteredPaymentOrder>>(stream, cancellationToken: cancellationToken)) ?? [];
    }

    internal async Task WriteUnsafeAsync(IReadOnlyList<RegisteredPaymentOrder> orders, CancellationToken cancellationToken = default)
    {
        var temporaryPath = $"{StoragePath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, orders, cancellationToken: cancellationToken);
        }
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }
}
