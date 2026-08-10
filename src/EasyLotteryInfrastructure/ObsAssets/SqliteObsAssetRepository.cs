using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using EasyLotteryApplication.ObsAssets;
using EasyLotteryDomain.Models.Obs;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.ObsAssets;

public sealed class SqliteObsAssetRepository(
    IOptions<StorageProviderOptions> options,
    IConfiguration configuration) : IObsAssetRepository
{
    private sealed class PackageManifest
    {
        public int Version { get; set; } = 1;
        public List<PackageAsset> Assets { get; set; } = [];
    }

    private sealed class PackageAsset
    {
        public ObsAsset Asset { get; set; } = new();
        public string Entry { get; set; } = "";
    }

    private sealed record ImportedAsset(ObsAsset Asset, byte[] Content);

    private readonly string _connectionString = options.Value.ConnectionString!;
    private readonly long _maxAssetBytes = long.TryParse(configuration["Storage:MaxAssetBytes"], out var configured)
        ? Math.Clamp(configured, 64 * 1024, 50 * 1024 * 1024)
        : 10 * 1024 * 1024;

    public async Task<IReadOnlyList<ObsAsset>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM obs_assets ORDER BY rowid DESC";
        var assets = new List<ObsAsset>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            assets.Add(Deserialize(reader.GetString(0)));
        return assets;
    }

    public async Task<ObsAsset?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await ReadAssetAsync(connection, id, cancellationToken);
    }

    public async Task<ObsAsset> SaveAsync(ObsAsset asset, Stream content, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadContentAsync(content, cancellationToken);
        var fileName = SanitizeFileName(asset.FileName);
        ValidateContentType(asset.Kind, asset.ContentType);
        var id = asset.Id == Guid.Empty ? Guid.NewGuid() : asset.Id;
        await using var connection = await OpenAsync(cancellationToken);
        var existing = await ReadAssetAsync(connection, id, cancellationToken);
        NormalizeAsset(asset, id, fileName, bytes, existing);
        await UpsertAsync(connection, asset, bytes, cancellationToken);
        return asset;
    }

    public async Task<Stream?> OpenReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT content FROM obs_assets WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is byte[] bytes ? new MemoryStream(bytes, writable: false) : null;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var asset = await ReadAssetAsync(connection, id, cancellationToken);
        if (asset is null) return false;
        if (asset.ReferencedBy.Count > 0)
            throw new InvalidOperationException("資產仍被使用中，請先移除資產關聯後再刪除。");

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM obs_assets WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task<Stream> ExportPackageAsync(CancellationToken cancellationToken = default)
    {
        var assets = await ListAsync(cancellationToken);
        var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = new PackageManifest();
            foreach (var asset in assets)
            {
                await using var content = await OpenReadAsync(asset.Id, cancellationToken)
                    ?? throw new InvalidDataException($"找不到資產內容：{asset.Id:D}。");
                var entryName = $"assets/{asset.Id:N}.bin";
                await using (var target = archive.CreateEntry(entryName, CompressionLevel.Optimal).Open())
                    await content.CopyToAsync(target, cancellationToken);
                manifest.Assets.Add(new PackageAsset { Asset = asset, Entry = entryName });
            }

            await using var manifestWriter = new StreamWriter(archive.CreateEntry("manifest.json").Open());
            await manifestWriter.WriteAsync(JsonSerializer.Serialize(manifest).AsMemory(), cancellationToken);
        }
        package.Position = 0;
        return package;
    }

    public async Task<IReadOnlyList<ObsAsset>> ImportPackageAsync(Stream package, CancellationToken cancellationToken = default)
    {
        var imported = await ReadPackageAsync(package, cancellationToken);
        if (imported.Count == 0) return [];

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var item in imported)
        {
            var existing = await ReadAssetAsync(connection, transaction, item.Asset.Id, cancellationToken);
            var fileName = SanitizeFileName(item.Asset.FileName);
            NormalizeAsset(item.Asset, item.Asset.Id, fileName, item.Content, existing);
            await UpsertAsync(connection, transaction, item.Asset, item.Content, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return imported.Select(item => item.Asset).ToList();
    }

    private async Task<List<ImportedAsset>> ReadPackageAsync(Stream package, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("資產包缺少 manifest.json。");
        PackageManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
            manifest = await JsonSerializer.DeserializeAsync<PackageManifest>(
                manifestStream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        if (manifest is null || manifest.Version != 1 || manifest.Assets is null)
            throw new InvalidDataException("不支援的資產包格式。");
        if (manifest.Assets.Count > 100) throw new InvalidDataException("資產包最多只能包含 100 筆資產。");

        var imported = new List<ImportedAsset>();
        var ids = new HashSet<Guid>();
        long totalBytes = 0;
        foreach (var packageAsset in manifest.Assets)
        {
            var asset = packageAsset.Asset ?? throw new InvalidDataException("資產包包含空的資產描述。");
            if (asset.Id == Guid.Empty || !ids.Add(asset.Id)) throw new InvalidDataException("資產包包含重複或無效的資產 UUID。");
            var expectedEntry = $"assets/{asset.Id:N}.bin";
            if (!string.Equals(packageAsset.Entry, expectedEntry, StringComparison.Ordinal)) throw new InvalidDataException("資產內容路徑無效。");
            ValidateContentType(asset.Kind, asset.ContentType);
            var entry = archive.GetEntry(expectedEntry) ?? throw new InvalidDataException($"找不到資產內容：{asset.Id:D}。");
            if (entry.Length <= 0 || entry.Length > _maxAssetBytes || (totalBytes += entry.Length) > 100 * 1024 * 1024)
                throw new InvalidDataException("資產包超過大小限制。");
            await using var entryStream = entry.Open();
            using var content = new MemoryStream();
            await entryStream.CopyToAsync(content, cancellationToken);
            var bytes = content.ToArray();
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), asset.Sha256, StringComparison.OrdinalIgnoreCase)
                || asset.Length != bytes.LongLength)
                throw new InvalidDataException($"資產校驗失敗：{asset.FileName}。");
            asset.ReferencedBy = (asset.ReferencedBy ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            imported.Add(new ImportedAsset(asset, bytes));
        }
        return imported;
    }

    private async Task<byte[]> ReadContentAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > _maxAssetBytes) throw new InvalidOperationException("資產檔案超過大小限制。");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        if (buffer.Length == 0) throw new InvalidOperationException("資產檔案不可為空。");
        return buffer.ToArray();
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task<ObsAsset?> ReadAssetAsync(SqliteConnection connection, Guid id, CancellationToken cancellationToken) =>
        await ReadAssetAsync(connection, transaction: null, id, cancellationToken);

    private async Task<ObsAsset?> ReadAssetAsync(SqliteConnection connection, SqliteTransaction? transaction, Guid id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload_json FROM obs_assets WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? Deserialize(json) : null;
    }

    private async Task UpsertAsync(SqliteConnection connection, ObsAsset asset, byte[] content, CancellationToken cancellationToken) =>
        await UpsertAsync(connection, transaction: null, asset, content, cancellationToken);

    private async Task UpsertAsync(SqliteConnection connection, SqliteTransaction? transaction, ObsAsset asset, byte[] content, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO obs_assets (id, payload_json, content) VALUES ($id, $payload, $content) ON CONFLICT(id) DO UPDATE SET payload_json = excluded.payload_json, content = excluded.content";
        command.Parameters.AddWithValue("$id", asset.Id.ToString("D"));
        command.Parameters.AddWithValue("$payload", Serialize(asset));
        command.Parameters.AddWithValue("$content", content);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void NormalizeAsset(ObsAsset asset, Guid id, string fileName, byte[] content, ObsAsset? existing)
    {
        asset.Id = id;
        asset.FileName = fileName;
        asset.ContentType = string.IsNullOrWhiteSpace(asset.ContentType) ? "application/octet-stream" : asset.ContentType.Trim().ToLowerInvariant();
        asset.Length = content.LongLength;
        asset.Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        asset.CreatedAtUtc = existing?.CreatedAtUtc ?? DateTimeOffset.UtcNow;
        asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
        asset.ReferencedBy = (asset.ReferencedBy ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (asset.Kind == ObsAssetKind.Image)
            (asset.Width, asset.Height) = ReadImageDimensions(content);
    }

    private static ObsAsset Deserialize(string value) =>
        JsonSerializer.Deserialize<ObsAsset>(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("SQLite OBS 資產 metadata 無法解析。");

    private static string Serialize(ObsAsset asset) => JsonSerializer.Serialize(asset, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string SanitizeFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..") throw new InvalidOperationException("資產檔名無效。");
        return safe.Length > 160 ? safe[..160] : safe;
    }

    private static void ValidateContentType(ObsAssetKind kind, string? contentType)
    {
        var normalized = (contentType ?? "").Trim().ToLowerInvariant();
        var valid = kind switch
        {
            ObsAssetKind.Image => normalized.StartsWith("image/", StringComparison.Ordinal),
            ObsAssetKind.Audio => normalized.StartsWith("audio/", StringComparison.Ordinal),
            ObsAssetKind.Video => normalized == "video/webm",
            ObsAssetKind.Model => normalized is "model/gltf+json" or "model/gltf-binary" or "application/octet-stream" or "application/json" || normalized.EndsWith("+json", StringComparison.Ordinal),
            _ => normalized.Length > 0
        };
        if (!valid) throw new InvalidOperationException($"資產用途與 MIME 類型不相容：{kind}／{contentType}。");
    }

    private static (int? Width, int? Height) ReadImageDimensions(byte[] bytes)
    {
        if (bytes.Length < 24) return (null, null);
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        if (bytes.Length >= 10 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F')
            return (BitConverter.ToUInt16(bytes, 6), BitConverter.ToUInt16(bytes, 8));
        return (null, null);
    }
}
