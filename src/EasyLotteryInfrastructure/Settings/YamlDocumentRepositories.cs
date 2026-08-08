using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.Settings;

public abstract class YamlDocumentRepositoryBase<T> where T : class, IYamlVersionedDocument, new()
{
    private readonly IStorageGateProvider _storageGates;
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly int _backupCount;

    protected YamlDocumentRepositoryBase(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates, string fileName)
    {
        _storageGates = storageGates;
        var storageDirectory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(storageDirectory);
        StoragePath = Path.Combine(storageDirectory, fileName);
        _backupCount = int.TryParse(configuration["Storage:BackupCount"], out var configuredBackupCount)
            ? Math.Clamp(configuredBackupCount, 1, 20)
            : 5;
        EnsureExists();
    }

    public string StoragePath { get; }

    public async Task<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        return await ReadUnsafeAsync(cancellationToken);
    }

    public async Task SaveAsync(T document, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        await SaveUnsafeAsync(document, cancellationToken);
    }

    internal async Task<T> ReadUnsafeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(StoragePath)) return new T();

        var yaml = await File.ReadAllTextAsync(StoragePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw Quarantine("YAML 檔案是空的，已停止讀取以避免覆蓋資料。", null);
        }

        T document;
        try
        {
            document = _deserializer.Deserialize<T>(yaml)
                ?? throw new InvalidDataException("YAML 文件沒有內容。");
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidDataException)
        {
            throw Quarantine("YAML 檔案格式損壞，已隔離檔案。", exception);
        }

        var migrated = YamlDocumentMigrations.Apply(document, out var changed);
        if (changed)
        {
            await SaveUnsafeAsync(migrated, cancellationToken);
        }

        return migrated;
    }

    internal async Task SaveUnsafeAsync(T document, CancellationToken cancellationToken = default)
    {
        document = YamlDocumentMigrations.Apply(document, out _);
        CreateBackup();
        var temporaryPath = $"{StoragePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, _serializer.Serialize(document), cancellationToken);
            File.Move(temporaryPath, StoragePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public IReadOnlyList<StorageBackupInfo> ListBackups()
    {
        var directory = Path.GetDirectoryName(StoragePath) ?? ".";
        var prefix = Path.GetFileName(StoragePath) + ".bak.";
        return Directory.EnumerateFiles(directory, prefix + "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new StorageBackupInfo(file.Name, file.LastWriteTimeUtc, file.Length))
            .ToList();
    }

    public async Task RestoreBackupAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(StoragePath) ?? ".";
        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
        var prefix = Path.GetFileName(StoragePath) + ".bak.";
        if (!Path.GetFileName(fullPath).StartsWith(prefix, StringComparison.Ordinal) || !File.Exists(fullPath))
            throw new FileNotFoundException("找不到指定的 YAML 備份。", fileName);

        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath, fullPath);
        CreateBackup();
        File.Copy(fullPath, StoragePath, overwrite: true);
        await ReadUnsafeAsync(cancellationToken);
    }

    private void EnsureExists()
    {
        if (File.Exists(StoragePath))
        {
            return;
        }

        var temporaryPath = $"{StoragePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, _serializer.Serialize(new T()));
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }

    private void CreateBackup()
    {
        if (!File.Exists(StoragePath)) return;
        var backupPath = $"{StoragePath}.bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}";
        File.Copy(StoragePath, backupPath, overwrite: false);
        foreach (var oldBackup in ListBackups().Skip(_backupCount))
        {
            var path = Path.Combine(Path.GetDirectoryName(StoragePath) ?? ".", oldBackup.FileName);
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    private YamlStorageException Quarantine(string message, Exception? exception)
    {
        string? quarantinedPath = null;
        try
        {
            if (File.Exists(StoragePath))
            {
                quarantinedPath = $"{StoragePath}.corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}";
                File.Move(StoragePath, quarantinedPath, overwrite: false);
            }
        }
        catch (IOException) { }

        return new YamlStorageException(message, StoragePath, quarantinedPath, exception);
    }
}

public sealed class YamlSettingsDocumentRepository : YamlDocumentRepositoryBase<SettingsYamlDocument>, ISettingsYamlDocumentRepository
{
    public YamlSettingsDocumentRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
        : base(configuration, environment, storageGates, "settings.yaml")
    {
        MigrateLegacyDocumentIfNeeded();
    }

    private void MigrateLegacyDocumentIfNeeded()
    {
        var directory = Path.GetDirectoryName(StoragePath) ?? ".";
        var activitiesPath = Path.Combine(directory, "activities.yaml");
        var resultsPath = Path.Combine(directory, "activity-results.yaml");
        if (!File.Exists(StoragePath)) return;

        EasyLotteryConfigDocument legacy;
        try
        {
            var yaml = File.ReadAllText(StoragePath);
            legacy = YamlSerialization.CreateDeserializerBuilder().Build().Deserialize<EasyLotteryConfigDocument>(yaml)
                ?? new EasyLotteryConfigDocument();
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return;
        }

        if (!HasLegacyActivityData(legacy)) return;

        if (!TryReadExisting(activitiesPath, out ActivitiesYamlDocument existingActivities) ||
            !TryReadExisting(resultsPath, out ActivityResultsYamlDocument existingResults)) return;

        var parts = YamlRepositoryMapper.Split(legacy);
        var backupPath = $"{StoragePath}.legacy.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.bak";
        File.Copy(StoragePath, backupPath, overwrite: false);
        if (!HasActivityData(existingActivities)) WriteAtomically(activitiesPath, YamlSerialization.CreateSerializerBuilder().Build().Serialize(parts.Activities));
        if (!HasResultData(existingResults)) WriteAtomically(resultsPath, YamlSerialization.CreateSerializerBuilder().Build().Serialize(parts.Results));
        WriteAtomically(StoragePath, YamlSerialization.CreateSerializerBuilder().Build().Serialize(parts.Settings));
    }

    private static bool HasLegacyActivityData(EasyLotteryConfigDocument document) =>
        document.PokeTemplates is { Count: > 0 } ||
        document.RouletteTemplates is { Count: > 0 } ||
        document.DonateLotteryActivities is { Count: > 0 } ||
        document.ActivityResults is { Count: > 0 } ||
        document.DonateLotteryDrawRecords is { Count: > 0 } ||
        document.ProcessedDonatePaymentIds is { Count: > 0 };

    private static bool HasActivityData(ActivitiesYamlDocument document) =>
        document.PokeTemplates is { Count: > 0 } ||
        document.RouletteTemplates is { Count: > 0 } ||
        document.DonateLotteryActivities is { Count: > 0 };

    private static bool HasResultData(ActivityResultsYamlDocument document) =>
        document.ActivityResults is { Count: > 0 } ||
        document.DonateLotteryDrawRecords is { Count: > 0 } ||
        document.ProcessedDonatePaymentIds is { Count: > 0 };

    private static bool TryReadExisting<T>(string path, out T document) where T : class, new()
    {
        document = new T();
        if (!File.Exists(path)) return true;

        var yaml = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(yaml)) return true;

        try
        {
            document = YamlSerialization.CreateDeserializerBuilder().Build().Deserialize<T>(yaml) ?? new T();
            return true;
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return false;
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.migration.tmp";
        try
        {
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

public sealed class YamlActivitiesDocumentRepository : YamlDocumentRepositoryBase<ActivitiesYamlDocument>, IActivitiesYamlDocumentRepository
{
    public YamlActivitiesDocumentRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
        : base(configuration, environment, storageGates, "activities.yaml")
    {
    }
}

public sealed class YamlActivityResultsDocumentRepository : YamlDocumentRepositoryBase<ActivityResultsYamlDocument>, IActivityResultsYamlDocumentRepository
{
    public YamlActivityResultsDocumentRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
        : base(configuration, environment, storageGates, "activity-results.yaml")
    {
    }
}
