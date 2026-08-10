using System.Security.Cryptography;
using System.Text;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.Settings;

public sealed class SqliteConfigRepository(
    SqliteConfigStore store,
    IOptions<StorageProviderOptions> options,
    ConfigSecretRedactor secrets) : IEasyLotteryConfigRepository
{
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly string? _databasePath = ResolveDatabasePath(options.Value.ConnectionString);

    public async Task<string> ReadForBrowserAsync(CancellationToken cancellationToken) =>
        (await ReadForBrowserSnapshotAsync(cancellationToken)).Yaml;

    public async Task<ConfigBrowserSnapshot> ReadForBrowserSnapshotAsync(CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        var yaml = secrets.RedactForBrowser(_serializer.Serialize(document));
        return new ConfigBrowserSnapshot(yaml, ComputeETag(yaml));
    }

    public async Task SaveBrowserUpdateAsync(string submittedYaml, CancellationToken cancellationToken) =>
        await SaveBrowserUpdateAsync(submittedYaml, expectedETag: null, cancellationToken);

    public async Task SaveBrowserUpdateAsync(string submittedYaml, string? expectedETag, CancellationToken cancellationToken)
    {
        var current = await store.LoadAsync(cancellationToken);
        var currentYaml = _serializer.Serialize(current);
        var actualETag = ComputeETag(secrets.RedactForBrowser(currentYaml));
        if (!string.IsNullOrWhiteSpace(expectedETag)
            && expectedETag != "*"
            && !string.Equals(expectedETag.Trim(), actualETag, StringComparison.Ordinal))
        {
            throw new ConfigurationConcurrencyException(expectedETag, actualETag);
        }

        var mergedYaml = secrets.MergeBrowserUpdate(currentYaml, submittedYaml);
        var document = string.IsNullOrWhiteSpace(mergedYaml)
            ? new EasyLotteryConfigDocument()
            : _deserializer.Deserialize<EasyLotteryConfigDocument>(mergedYaml) ?? new EasyLotteryConfigDocument();
        CreateBackup();
        await store.SaveAsync(document, cancellationToken);
    }

    public IReadOnlyList<ConfigBackupInfo> ListBackups()
    {
        if (_databasePath is null) return [];
        var directory = GetBackupDirectory();
        if (!Directory.Exists(directory)) return [];
        var prefix = Path.GetFileName(_databasePath) + ".bak.";
        return Directory.EnumerateFiles(directory, prefix + "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new ConfigBackupInfo(file.Name, file.LastWriteTimeUtc, file.Length))
            .ToList();
    }

    public async Task RestoreBackupAsync(string id, CancellationToken cancellationToken)
    {
        if (_databasePath is null)
            throw new InvalidOperationException("SQLite memory database 不支援備份還原。");
        var prefix = Path.GetFileName(_databasePath) + ".bak.";
        if (string.IsNullOrWhiteSpace(id) || id != Path.GetFileName(id) || !id.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException("備份識別碼無效。", nameof(id));

        var backupPath = Path.Combine(GetBackupDirectory(), id);
        if (!File.Exists(backupPath)) throw new FileNotFoundException("找不到指定的 SQLite 備份。", id);
        CreateBackup();
        File.Copy(backupPath, _databasePath, overwrite: true);
        await store.LoadAsync(cancellationToken);
    }

    public Task<EasyLotteryConfigDocument> ReadAsync(CancellationToken cancellationToken) => store.LoadAsync(cancellationToken);

    public async Task<T> UpdateAsync<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        var result = update(document);
        CreateBackup();
        await store.SaveAsync(document, cancellationToken);
        return result;
    }

    private void CreateBackup()
    {
        if (_databasePath is null || !File.Exists(_databasePath)) return;
        var directory = GetBackupDirectory();
        Directory.CreateDirectory(directory);
        var backupPath = Path.Combine(directory, $"{Path.GetFileName(_databasePath)}.bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}");
        File.Copy(_databasePath, backupPath, overwrite: false);
        foreach (var oldBackup in ListBackups().Skip(5))
            File.Delete(Path.Combine(directory, oldBackup.Id));
    }

    private string GetBackupDirectory() =>
        Path.Combine(Path.GetDirectoryName(_databasePath!) ?? ".", "config-backups");

    private static string ComputeETag(string yaml)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(yaml));
        return $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }

    private static string? ResolveDatabasePath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.Mode == SqliteOpenMode.Memory) return null;
        var dataSource = builder.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:" || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return null;
        return Path.GetFullPath(dataSource);
    }
}
