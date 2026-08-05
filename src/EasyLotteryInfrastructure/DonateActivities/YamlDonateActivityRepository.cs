using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using EasyLotteryInfrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.DonateActivities;

public sealed class YamlDonateActivityRepository : IDonateLotteryActivityRepository
{
    private readonly IStorageGateProvider _storageGates;
    private readonly IDonateActivityEventStore _eventStore;
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    // Activity snapshots must preserve explicit false values such as ShowDonateInformation.
    // The shared serializer omits defaults, which would otherwise turn a saved false back
    // into the model's true default when the YAML is read again.
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

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
        var (normalized, _) = NormalizeActivities(replay);
        await WriteSnapshotUnsafeAsync(normalized, cancellationToken);
        return normalized;
    }

    internal async Task<IReadOnlyList<DonateLotteryActivity>> ReadSnapshotUnsafeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SnapshotPath))
        {
            return [];
        }

        var yaml = await File.ReadAllTextAsync(SnapshotPath, cancellationToken);
        List<DonateLotteryActivity> activities;
        try
        {
            if (string.IsNullOrWhiteSpace(yaml))
                throw new InvalidDataException("YAML 文件是空的。");
            activities = DeserializeActivities(yaml);
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidDataException or YamlStorageException)
        {
            throw QuarantineSnapshot("Donate 活動 YAML 格式損壞，已隔離檔案。", exception);
        }
        var (normalized, changed) = NormalizeActivities(activities);
        if (changed)
        {
            await WriteSnapshotUnsafeAsync(normalized, cancellationToken);
        }

        return normalized;
    }

    internal async Task WriteSnapshotUnsafeAsync(IReadOnlyList<DonateLotteryActivity> activities, CancellationToken cancellationToken = default)
    {
        ActivitiesYamlDocument document;
        if (File.Exists(SnapshotPath))
        {
            try
            {
                document = DeserializeActivitiesDocument(await File.ReadAllTextAsync(SnapshotPath, cancellationToken));
            }
            catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidDataException or YamlStorageException)
            {
                throw QuarantineSnapshot("Donate 活動 YAML 格式損壞，已隔離檔案。", exception);
            }
        }
        else
        {
            document = new ActivitiesYamlDocument();
        }
        document.DonateLotteryActivities = activities.OrderByDescending(activity => activity.Id).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath) ?? ".");
        CreateBackup();
        var temporaryPath = $"{SnapshotPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, _serializer.Serialize(document), cancellationToken);
        File.Move(temporaryPath, SnapshotPath, overwrite: true);
    }

    private List<DonateLotteryActivity> DeserializeActivities(string yaml) =>
        DeserializeActivitiesDocument(yaml).DonateLotteryActivities ?? [];

    private ActivitiesYamlDocument DeserializeActivitiesDocument(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return new ActivitiesYamlDocument();

        try
        {
            return _deserializer.Deserialize<ActivitiesYamlDocument>(yaml) ?? new ActivitiesYamlDocument();
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // Snapshots produced before YAML repositories were split used a root list.
            try
            {
                return new ActivitiesYamlDocument
                {
                    DonateLotteryActivities = _deserializer.Deserialize<List<DonateLotteryActivity>>(yaml) ?? []
                };
            }
            catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidDataException)
            {
                throw new YamlStorageException("Donate 活動 YAML 無法解析。", SnapshotPath, innerException: exception);
            }
        }
    }

    private void CreateBackup()
    {
        if (!File.Exists(SnapshotPath)) return;
        var backupPath = $"{SnapshotPath}.bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}";
        File.Copy(SnapshotPath, backupPath, overwrite: false);
        var directory = Path.GetDirectoryName(SnapshotPath) ?? ".";
        foreach (var oldBackup in Directory.EnumerateFiles(directory, Path.GetFileName(SnapshotPath) + ".bak.*")
                     .OrderByDescending(File.GetLastWriteTimeUtc).Skip(5))
        {
            try { File.Delete(oldBackup); } catch (IOException) { }
        }
    }

    private YamlStorageException QuarantineSnapshot(string message, Exception exception)
    {
        string? quarantinedPath = null;
        if (File.Exists(SnapshotPath))
        {
            quarantinedPath = $"{SnapshotPath}.corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}";
            File.Move(SnapshotPath, quarantinedPath, overwrite: false);
        }

        return new YamlStorageException(message, SnapshotPath, quarantinedPath, exception);
    }

    private static (List<DonateLotteryActivity> Activities, bool Changed) NormalizeActivities(IEnumerable<DonateLotteryActivity> activities)
    {
        var changed = false;
        var normalized = activities.Select(activity =>
        {
            if (activity.PublicId == Guid.Empty)
            {
                activity.PublicId = Guid.NewGuid();
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(activity.PolaroidTemplateKey))
            {
                activity.PolaroidTemplateKey = "classic";
                changed = true;
            }

            if (activity.ResultDisplayDurationSeconds <= 0)
            {
                activity.ResultDisplayDurationSeconds = 15;
                changed = true;
            }

            if (activity.AnimationDurationSeconds <= 0)
            {
                activity.AnimationDurationSeconds = 8;
                changed = true;
            }

            if (activity.WebmAnimationUrl is null)
            {
                activity.WebmAnimationUrl = "";
                changed = true;
            }

            if (activity.WebmPosterUrl is null)
            {
                activity.WebmPosterUrl = "";
                changed = true;
            }

            return activity;
        }).ToList();

        return (normalized, changed);
    }
}
