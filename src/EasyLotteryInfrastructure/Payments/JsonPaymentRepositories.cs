using System.Text.Json;
using EasyLotteryApplication.Payments;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryInfrastructure.Payments;

public sealed class JsonPaymentEventRepository : IPaymentEventRepository
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
}

public sealed class JsonPaymentOrderRepository : IPaymentOrderRepository
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
