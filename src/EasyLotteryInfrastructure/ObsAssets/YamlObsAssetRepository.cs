using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using EasyLotteryApplication.ObsAssets;
using EasyLotteryDomain.Models.Obs;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.ObsAssets;

/// <summary>以檔案系統保存 OBS 資產；metadata 與內容原子更新，未來可替換成雲端或 DB repository。</summary>
public sealed class YamlObsAssetRepository : IObsAssetRepository
{
    private sealed class Document
    {
        public int Version { get; set; } = 1;
        public List<ObsAsset> Assets { get; set; } = [];
    }

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

    private readonly IStorageGateProvider _storageGates;
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly long _maxAssetBytes;

    public YamlObsAssetRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
    {
        _storageGates = storageGates;
        var directory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        AssetDirectory = Path.Combine(directory, "obs-assets");
        MetadataPath = Path.Combine(directory, "obs-assets.yaml");
        Directory.CreateDirectory(AssetDirectory);
        _maxAssetBytes = long.TryParse(configuration["Storage:MaxAssetBytes"], out var configured)
            ? Math.Clamp(configured, 64 * 1024, 50 * 1024 * 1024)
            : 10 * 1024 * 1024;
        EnsureMetadataExists();
    }

    public string AssetDirectory { get; }
    public string MetadataPath { get; }
    public long MaxAssetBytes => _maxAssetBytes;

    public async Task<IReadOnlyList<ObsAsset>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, MetadataPath);
        return (await ReadUnsafeAsync(cancellationToken)).Assets
            .OrderByDescending(asset => asset.UpdatedAtUtc)
            .ToList();
    }

    public async Task<ObsAsset?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, MetadataPath);
        return (await ReadUnsafeAsync(cancellationToken)).Assets.FirstOrDefault(asset => asset.Id == id);
    }

    public async Task<ObsAsset> SaveAsync(ObsAsset asset, Stream content, CancellationToken cancellationToken = default)
    {
        if (asset.Length > _maxAssetBytes)
            throw new InvalidOperationException($"資產大小不可超過 {_maxAssetBytes / 1024 / 1024} MB。");

        var fileName = SanitizeFileName(asset.FileName);
        ValidateContentType(asset.Kind, asset.ContentType);
        var id = asset.Id == Guid.Empty ? Guid.NewGuid() : asset.Id;
        var contentPath = GetContentPath(id);
        var temporaryPath = $"{contentPath}.{Guid.NewGuid():N}.tmp";
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, MetadataPath, contentPath);
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > _maxAssetBytes) throw new InvalidOperationException("資產檔案超過大小限制。");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                if (total == 0) throw new InvalidOperationException("資產檔案不可為空。");
                asset.Length = total;
                asset.Sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            File.Move(temporaryPath, contentPath, overwrite: true);
            var document = await ReadUnsafeAsync(cancellationToken);
            var existing = document.Assets.FirstOrDefault(item => item.Id == id);
            asset.Id = id;
            asset.FileName = fileName;
            asset.ContentType = string.IsNullOrWhiteSpace(asset.ContentType) ? "application/octet-stream" : asset.ContentType.Trim().ToLowerInvariant();
            asset.CreatedAtUtc = existing?.CreatedAtUtc ?? DateTimeOffset.UtcNow;
            asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
            asset.ReferencedBy = asset.ReferencedBy.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (asset.Kind == ObsAssetKind.Image)
            {
                (asset.Width, asset.Height) = ImageDimensions.TryRead(contentPath);
            }

            if (existing is not null) document.Assets.Remove(existing);
            document.Assets.Add(asset);
            await WriteUnsafeAsync(document, cancellationToken);
            return asset;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<Stream?> OpenReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await GetAsync(id, cancellationToken);
        if (asset is null) return null;
        var path = GetContentPath(id);
        if (!File.Exists(path)) return null;
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, MetadataPath, GetContentPath(id));
        var document = await ReadUnsafeAsync(cancellationToken);
        var asset = document.Assets.FirstOrDefault(item => item.Id == id);
        if (asset is null) return false;
        if (asset.ReferencedBy.Count > 0)
            throw new InvalidOperationException("資產仍被使用中，請先移除資產關聯後再刪除。");
        document.Assets.Remove(asset);
        await WriteUnsafeAsync(document, cancellationToken);
        var path = GetContentPath(id);
        if (File.Exists(path)) File.Delete(path);
        return true;
    }

    public async Task<Stream> ExportPackageAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, MetadataPath);
        var document = await ReadUnsafeAsync(cancellationToken);
        var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = new PackageManifest();
            foreach (var asset in document.Assets)
            {
                var path = GetContentPath(asset.Id);
                if (!File.Exists(path)) throw new InvalidDataException($"找不到資產內容：{asset.Id:D}。");
                var entryName = $"assets/{asset.Id:N}.bin";
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using (var source = File.OpenRead(path))
                await using (var target = entry.Open())
                    await source.CopyToAsync(target, cancellationToken);
                manifest.Assets.Add(new PackageAsset { Asset = asset, Entry = entryName });
            }

            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
            await using var manifestStream = new StreamWriter(manifestEntry.Open());
            await manifestStream.WriteAsync(JsonSerializer.Serialize(manifest).AsMemory(), cancellationToken);
        }

        package.Position = 0;
        return package;
    }

    public async Task<IReadOnlyList<ObsAsset>> ImportPackageAsync(Stream package, CancellationToken cancellationToken = default)
    {
        var imported = await ReadPackageAsync(package, cancellationToken);
        if (imported.Count == 0) return [];

        await using var gate = await _storageGates.AcquireAsync(
            cancellationToken,
            new[] { MetadataPath }.Concat(imported.Select(item => GetContentPath(item.Asset.Id))).ToArray());
        var stagingDirectory = Path.Combine(AssetDirectory, $".import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);
        var backups = new Dictionary<string, string>();
        var metadataBackup = Path.Combine(stagingDirectory, "metadata.yaml");
        try
        {
            File.Copy(MetadataPath, metadataBackup, overwrite: true);
            foreach (var item in imported)
            {
                var target = GetContentPath(item.Asset.Id);
                if (File.Exists(target))
                {
                    var backup = Path.Combine(stagingDirectory, $"{item.Asset.Id:N}.bak");
                    File.Copy(target, backup, overwrite: false);
                    backups[target] = backup;
                }

                var staged = Path.Combine(stagingDirectory, $"{item.Asset.Id:N}.bin");
                await File.WriteAllBytesAsync(staged, item.Content, cancellationToken);
                item.StagedPath = staged;
            }

            var document = await ReadUnsafeAsync(cancellationToken);
            foreach (var item in imported)
            {
                var target = GetContentPath(item.Asset.Id);
                File.Move(item.StagedPath!, target, overwrite: true);
                item.Asset.Length = item.Content.LongLength;
                item.Asset.Sha256 = Convert.ToHexString(SHA256.HashData(item.Content)).ToLowerInvariant();
                item.Asset.FileName = SanitizeFileName(item.Asset.FileName);
                item.Asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
                if (item.Asset.Kind == ObsAssetKind.Image)
                    (item.Asset.Width, item.Asset.Height) = ImageDimensions.TryRead(target);
                document.Assets.RemoveAll(asset => asset.Id == item.Asset.Id);
                document.Assets.Add(item.Asset);
            }

            await WriteUnsafeAsync(document, cancellationToken);
            return imported.Select(item => item.Asset).ToList();
        }
        catch
        {
            if (File.Exists(metadataBackup)) File.Copy(metadataBackup, MetadataPath, overwrite: true);
            foreach (var item in imported)
            {
                var target = GetContentPath(item.Asset.Id);
                if (backups.TryGetValue(target, out var backup)) File.Copy(backup, target, overwrite: true);
                else if (File.Exists(target)) File.Delete(target);
            }
            throw;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory)) Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    private async Task<List<ImportedAsset>> ReadPackageAsync(Stream package, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("資產包缺少 manifest.json。");
        PackageManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
            manifest = await JsonSerializer.DeserializeAsync<PackageManifest>(manifestStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        if (manifest is null || manifest.Version != 1 || manifest.Assets is null) throw new InvalidDataException("不支援的資產包格式。");
        if (manifest.Assets.Count > 100) throw new InvalidDataException("資產包最多只能包含 100 筆資產。");

        var imported = new List<ImportedAsset>();
        var ids = new HashSet<Guid>();
        long totalBytes = 0;
        foreach (var packageAsset in manifest.Assets)
        {
            if (packageAsset is null) throw new InvalidDataException("資產包包含空的資產項目。");
            var asset = packageAsset.Asset ?? throw new InvalidDataException("資產包包含空的資產描述。");
            if (asset.Id == Guid.Empty || !ids.Add(asset.Id)) throw new InvalidDataException("資產包包含重複或無效的資產 UUID。");
            if (string.IsNullOrWhiteSpace(asset.FileName)) throw new InvalidDataException("資產包包含無效的檔名。");
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
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase) || asset.Length != bytes.LongLength)
                throw new InvalidDataException($"資產校驗失敗：{asset.FileName}。");
            asset.ReferencedBy = (asset.ReferencedBy ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            imported.Add(new ImportedAsset(asset, bytes));
        }

        return imported;
    }

    private sealed class ImportedAsset(ObsAsset asset, byte[] content)
    {
        public ObsAsset Asset { get; } = asset;
        public byte[] Content { get; } = content;
        public string? StagedPath { get; set; }
    }

    private async Task<Document> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(MetadataPath)) return new Document();
        var yaml = await File.ReadAllTextAsync(MetadataPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(yaml)) return new Document();
        var document = _deserializer.Deserialize<Document>(yaml) ?? new Document();
        document.Assets ??= [];
        foreach (var asset in document.Assets)
        {
            asset.ReferencedBy ??= [];
        }
        return document;
    }

    private async Task WriteUnsafeAsync(Document document, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{MetadataPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, _serializer.Serialize(document), cancellationToken);
            File.Move(temporaryPath, MetadataPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private string GetContentPath(Guid id) => Path.Combine(AssetDirectory, $"{id:N}.bin");

    private void EnsureMetadataExists()
    {
        if (!File.Exists(MetadataPath)) File.WriteAllText(MetadataPath, _serializer.Serialize(new Document()));
    }

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

    private static class ImageDimensions
    {
        public static (int? Width, int? Height) TryRead(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                Span<byte> header = stackalloc byte[32];
                if (stream.Read(header) < 24) return (null, null);
                if (header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                    return (ReadInt32(header[16..20]), ReadInt32(header[20..24]));
                if (header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F')
                    return (BitConverter.ToUInt16(header[6..8]), BitConverter.ToUInt16(header[8..10]));
            }
            catch (IOException) { }
            return (null, null);
        }

        private static int ReadInt32(ReadOnlySpan<byte> bytes) =>
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes);
    }
}
