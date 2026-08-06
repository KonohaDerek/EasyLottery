using System.Text.Json;
using EasyLotteryApplication.Payments;
using EasyLotteryDomain.Models.Overtime;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryInfrastructure.Payments;

public sealed class JsonOvertimeFeedRepository : IOvertimeFeedRepository, IOvertimeFeedMutationRepository
{
    private readonly IStorageGateProvider _storageGates;
    public string StoragePath { get; }

    public JsonOvertimeFeedRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
    {
        _storageGates = storageGates;
        var storageDirectory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(storageDirectory);
        StoragePath = Path.Combine(storageDirectory, "easy-lottery-overtime-feed.json");
    }

    public async Task<IReadOnlyList<OvertimeSupportEvent>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        return await ReadUnsafeAsync(cancellationToken);
    }

    public async Task SaveAsync(IReadOnlyList<OvertimeSupportEvent> events, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        await WriteUnsafeAsync(events, cancellationToken);
    }

    public async Task MutateAsync(Func<List<OvertimeSupportEvent>, Task> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        var events = (await ReadUnsafeAsync(cancellationToken)).ToList();
        await mutation(events);
        await WriteUnsafeAsync(events, cancellationToken);
    }

    internal async Task<IReadOnlyList<OvertimeSupportEvent>> ReadUnsafeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(StoragePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(StoragePath);
        return (await JsonSerializer.DeserializeAsync<List<OvertimeSupportEvent>>(stream, cancellationToken: cancellationToken)) ?? [];
    }

    internal async Task WriteUnsafeAsync(IReadOnlyList<OvertimeSupportEvent> events, CancellationToken cancellationToken = default)
    {
        var temporaryPath = $"{StoragePath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, events, cancellationToken: cancellationToken);
        }
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }
}
