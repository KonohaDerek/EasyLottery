using System.Security.Cryptography;
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
