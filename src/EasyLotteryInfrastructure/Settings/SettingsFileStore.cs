using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryApplication.Settings;
using EasyLotteryInfrastructure.Storage;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.Settings;

/// <summary>YAML-backed repository for the EasyLottery configuration.</summary>
public sealed class SettingsFileStore : IEasyLotteryConfigRepository, IEasyLotteryConfigStore
{
    private readonly ConfigSecretRedactor _secrets;
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly YamlSettingsDocumentRepository _settingsRepository;
    private readonly YamlActivitiesDocumentRepository _activitiesRepository;
    private readonly YamlActivityResultsDocumentRepository _resultsRepository;
    private readonly IStorageGateProvider _storageGates;
    private readonly int _backupCount;
    private readonly string _backupDirectory;
    private readonly string _pendingTransactionPath;

    public string ConfigPath { get; }

    public string ActivitiesPath { get; }

    public string ActivityResultsPath { get; }

    public SettingsFileStore(
        IConfiguration configuration,
        ConfigSecretRedactor secrets,
        YamlSettingsDocumentRepository settingsRepository,
        YamlActivitiesDocumentRepository activitiesRepository,
        YamlActivityResultsDocumentRepository resultsRepository,
        IStorageGateProvider storageGates)
    {
        _secrets = secrets;
        _settingsRepository = settingsRepository;
        _activitiesRepository = activitiesRepository;
        _resultsRepository = resultsRepository;
        _storageGates = storageGates;
        ConfigPath = settingsRepository.StoragePath;
        ActivitiesPath = activitiesRepository.StoragePath;
        ActivityResultsPath = resultsRepository.StoragePath;
        var storageDirectory = Path.GetDirectoryName(ConfigPath) ?? ".";
        _backupDirectory = Path.Combine(storageDirectory, "config-backups");
        _pendingTransactionPath = Path.Combine(_backupDirectory, "pending-transaction");
        _backupCount = int.TryParse(configuration["Storage:BackupCount"], out var configuredBackupCount)
            ? Math.Clamp(configuredBackupCount, 1, 20)
            : 5;
    }

    public async Task<string> ReadForBrowserAsync(CancellationToken cancellationToken)
    {
        var snapshot = await ReadForBrowserSnapshotAsync(cancellationToken);
        return snapshot.Yaml;
    }

    public async Task<ConfigBrowserSnapshot> ReadForBrowserSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var gate = await AcquireAllAsync(cancellationToken);
        var document = await ReadMergedDocumentUnsafeAsync(cancellationToken);
        var yaml = _secrets.RedactForBrowser(_serializer.Serialize(document));
        return new ConfigBrowserSnapshot(yaml, ComputeETag());
    }

    public Task SaveBrowserUpdateAsync(string submittedYaml, CancellationToken cancellationToken) =>
        SaveBrowserUpdateAsync(submittedYaml, expectedETag: null, cancellationToken);

    public async Task SaveBrowserUpdateAsync(string submittedYaml, string? expectedETag, CancellationToken cancellationToken)
    {
        await using var gate = await AcquireAllAsync(cancellationToken);
        var currentDocument = await ReadMergedDocumentUnsafeAsync(cancellationToken);
        var actualETag = ComputeETag();
        if (!string.IsNullOrWhiteSpace(expectedETag)
            && expectedETag != "*"
            && !string.Equals(expectedETag.Trim(), actualETag, StringComparison.Ordinal))
        {
            throw new ConfigurationConcurrencyException(expectedETag, actualETag);
        }

        var currentYaml = _serializer.Serialize(currentDocument);
        var mergedYaml = _secrets.MergeBrowserUpdate(currentYaml, submittedYaml);
        var document = string.IsNullOrWhiteSpace(mergedYaml)
            ? new EasyLotteryConfigDocument()
            : _deserializer.Deserialize<EasyLotteryConfigDocument>(mergedYaml) ?? new EasyLotteryConfigDocument();
        await SaveSplitDocumentsUnsafeAsync(document, cancellationToken);
    }

    public IReadOnlyList<ConfigBackupInfo> ListBackups()
    {
        if (!Directory.Exists(_backupDirectory)) return [];
        return Directory.EnumerateDirectories(_backupDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var directory = new DirectoryInfo(path);
                var files = directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToArray();
                return new ConfigBackupInfo(directory.Name, directory.LastWriteTimeUtc, files.Sum(file => file.Length));
            })
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task RestoreBackupAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || id != Path.GetFileName(id))
            throw new ArgumentException("備份識別碼無效。", nameof(id));

        var backupDirectory = Path.Combine(_backupDirectory, id);
        if (!Directory.Exists(backupDirectory))
            throw new FileNotFoundException("找不到指定的設定備份。", id);

        await using var gate = await AcquireAllAsync(cancellationToken);
        RecoverPendingTransactionUnsafe();
        CreateBackupSetUnsafe();
        var sources = new[]
        {
            (Source: Path.Combine(backupDirectory, Path.GetFileName(ConfigPath)), Target: ConfigPath),
            (Source: Path.Combine(backupDirectory, Path.GetFileName(ActivitiesPath)), Target: ActivitiesPath),
            (Source: Path.Combine(backupDirectory, Path.GetFileName(ActivityResultsPath)), Target: ActivityResultsPath)
        };
        if (sources.Any(item => !File.Exists(item.Source)))
            throw new InvalidDataException("備份缺少完整的設定文件。 ");

        var temporaryFiles = new List<(string Temp, string Target)>();
        try
        {
            foreach (var item in sources)
            {
                var temp = $"{item.Target}.{Guid.NewGuid():N}.restore.tmp";
                File.Copy(item.Source, temp, overwrite: false);
                temporaryFiles.Add((temp, item.Target));
            }

            foreach (var item in temporaryFiles)
                File.Move(item.Temp, item.Target, overwrite: true);

            await ReadMergedDocumentUnsafeAsync(cancellationToken);
        }
        finally
        {
            foreach (var item in temporaryFiles)
                if (File.Exists(item.Temp)) File.Delete(item.Temp);
        }
    }

    public Task<EasyLotteryConfigDocument> LoadAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(cancellationToken);

    public Task<EasyLotteryConfigDocument> ReadAsync(CancellationToken cancellationToken)
    {
        return ReadMergedDocumentAsync(cancellationToken);
    }

    public Task<T> UpdateAsync<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken)
    {
        return UpdateAsyncInternal(update, cancellationToken);
    }

    public async Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default)
    {
        await SaveSplitDocumentsAsync(document, cancellationToken);
    }

    private async Task<T> UpdateAsyncInternal<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken)
    {
        await using var gate = await AcquireAllAsync(cancellationToken);
        var document = await ReadMergedDocumentUnsafeAsync(cancellationToken);
        var result = update(document);
        await SaveSplitDocumentsUnsafeAsync(document, cancellationToken);
        return result;
    }

    private async Task<EasyLotteryConfigDocument> ReadMergedDocumentAsync(CancellationToken cancellationToken)
    {
        await using var gate = await AcquireAllAsync(cancellationToken);
        return await ReadMergedDocumentUnsafeAsync(cancellationToken);
    }

    private async Task<EasyLotteryConfigDocument> ReadMergedDocumentUnsafeAsync(CancellationToken cancellationToken)
    {
        RecoverPendingTransactionUnsafe();
        var settings = await _settingsRepository.ReadUnsafeAsync(cancellationToken);
        var activities = await _activitiesRepository.ReadUnsafeAsync(cancellationToken);
        var results = await _resultsRepository.ReadUnsafeAsync(cancellationToken);
        return YamlRepositoryMapper.Merge(settings, activities, results);
    }

    private async Task SaveSplitDocumentsAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken)
    {
        await using var gate = await AcquireAllAsync(cancellationToken);
        await SaveSplitDocumentsUnsafeAsync(document, cancellationToken);
    }

    private async Task SaveSplitDocumentsUnsafeAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken)
    {
        var backupId = CreateBackupSetUnsafe();
        var committed = false;
        try
        {
            if (backupId is not null)
                await File.WriteAllTextAsync(_pendingTransactionPath, backupId, cancellationToken);

            var parts = YamlRepositoryMapper.Split(document);
            await _settingsRepository.SaveUnsafeAsync(parts.Settings, cancellationToken);
            await _activitiesRepository.SaveUnsafeAsync(parts.Activities, cancellationToken);
            await _resultsRepository.SaveUnsafeAsync(parts.Results, cancellationToken);
            committed = true;
        }
        finally
        {
            if (committed && File.Exists(_pendingTransactionPath))
                File.Delete(_pendingTransactionPath);
        }
    }

    private ValueTask<IAsyncDisposable> AcquireAllAsync(CancellationToken cancellationToken) =>
        _storageGates.AcquireAsync(cancellationToken, ConfigPath, ActivitiesPath, ActivityResultsPath);

    private string ComputeETag()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in new[] { ConfigPath, ActivitiesPath, ActivityResultsPath })
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(path)));
            if (File.Exists(path))
            {
                hash.AppendData(File.ReadAllBytes(path));
            }
        }

        return $"\"{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}\"";
    }

    private string? CreateBackupSetUnsafe()
    {
        if (!File.Exists(ConfigPath) || !File.Exists(ActivitiesPath) || !File.Exists(ActivityResultsPath)) return null;
        Directory.CreateDirectory(_backupDirectory);
        var id = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        var targetDirectory = Path.Combine(_backupDirectory, id);
        Directory.CreateDirectory(targetDirectory);
        foreach (var path in new[] { ConfigPath, ActivitiesPath, ActivityResultsPath })
            File.Copy(path, Path.Combine(targetDirectory, Path.GetFileName(path)), overwrite: false);

        foreach (var oldBackup in ListBackups().Skip(_backupCount))
        {
            try { Directory.Delete(Path.Combine(_backupDirectory, oldBackup.Id), recursive: true); } catch (IOException) { }
        }

        return id;
    }

    private void RecoverPendingTransactionUnsafe()
    {
        if (!File.Exists(_pendingTransactionPath)) return;
        var id = File.ReadAllText(_pendingTransactionPath).Trim();
        if (string.IsNullOrWhiteSpace(id) || id != Path.GetFileName(id))
            throw new YamlStorageException("設定交易標記損壞，無法安全復原。", _pendingTransactionPath);

        var backupDirectory = Path.Combine(_backupDirectory, id);
        var sources = new[]
        {
            (Source: Path.Combine(backupDirectory, Path.GetFileName(ConfigPath)), Target: ConfigPath),
            (Source: Path.Combine(backupDirectory, Path.GetFileName(ActivitiesPath)), Target: ActivitiesPath),
            (Source: Path.Combine(backupDirectory, Path.GetFileName(ActivityResultsPath)), Target: ActivityResultsPath)
        };
        if (sources.Any(item => !File.Exists(item.Source)))
            throw new YamlStorageException("找不到設定交易的完整備份，已停止讀取以避免使用混合版本。", backupDirectory);

        foreach (var item in sources)
        {
            var temporaryPath = $"{item.Target}.{Guid.NewGuid():N}.recovery.tmp";
            File.Copy(item.Source, temporaryPath, overwrite: false);
            try { File.Move(temporaryPath, item.Target, overwrite: true); }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }

        File.Delete(_pendingTransactionPath);
    }
}
