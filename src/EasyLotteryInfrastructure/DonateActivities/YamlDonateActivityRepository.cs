using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.DonateActivities;

public sealed class YamlDonateActivityRepository : IDonateLotteryActivityRepository
{
    private readonly IStorageGateProvider _storageGates;
    private readonly IDonateActivityEventStore _eventStore;
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();

    public string SnapshotPath { get; }

    public YamlDonateActivityRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates, IDonateActivityEventStore eventStore)
    {
        _storageGates = storageGates;
        _eventStore = eventStore;
        var directory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);
        SnapshotPath = Path.Combine(directory, "activities.yaml");
    }

    public async Task<IReadOnlyList<DonateLotteryActivity>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, SnapshotPath);
        return await ReadSnapshotOrReplayUnsafeAsync(cancellationToken);
    }

    public async Task<DonateLotteryActivity?> GetByIdAsync(int activityId, CancellationToken cancellationToken = default)
    {
        var activities = await ListAsync(cancellationToken);
        return activities.FirstOrDefault(activity => activity.Id == activityId);
    }

    public async Task SaveSnapshotAsync(IReadOnlyList<DonateLotteryActivity> activities, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, SnapshotPath);
        await WriteSnapshotUnsafeAsync(activities, cancellationToken);
    }

    internal async Task<IReadOnlyList<DonateLotteryActivity>> ReadSnapshotOrReplayUnsafeAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(SnapshotPath))
        {
            return await ReadSnapshotUnsafeAsync(cancellationToken);
        }

        var replay = DonateActivityEventProjector.Rebuild(await _eventStore.ReadAllAsync(cancellationToken));
        await WriteSnapshotUnsafeAsync(replay, cancellationToken);
        return replay;
    }

    internal async Task<IReadOnlyList<DonateLotteryActivity>> ReadSnapshotUnsafeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SnapshotPath))
        {
            return [];
        }

        var yaml = await File.ReadAllTextAsync(SnapshotPath, cancellationToken);
        return string.IsNullOrWhiteSpace(yaml)
            ? []
            : _deserializer.Deserialize<List<DonateLotteryActivity>>(yaml) ?? [];
    }

    internal async Task WriteSnapshotUnsafeAsync(IReadOnlyList<DonateLotteryActivity> activities, CancellationToken cancellationToken = default)
    {
        var snapshot = activities.OrderByDescending(activity => activity.Id).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath) ?? ".");
        var temporaryPath = $"{SnapshotPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, _serializer.Serialize(snapshot), cancellationToken);
        File.Move(temporaryPath, SnapshotPath, overwrite: true);
    }
}
