using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using EasyLotteryApplication.ObsAssets;
using EasyLotteryDomain.Models.Obs;
using EasyLotteryInfrastructure.ObsAssets;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class ObsAssetRepositoryTests
{
    [TestMethod]
    public async Task SaveAsync_PersistsHashDimensionsAndContentSeparatelyFromMetadata()
    {
        var directory = CreateTempDirectory();
        try
        {
            var repository = CreateRepository(directory);
            var bytes = CreatePngHeader(320, 180);
            var saved = await repository.SaveAsync(
                new ObsAsset { FileName = "banner.png", ContentType = "image/png", Kind = ObsAssetKind.Image },
                new MemoryStream(bytes));

            Assert.AreEqual(320, saved.Width);
            Assert.AreEqual(180, saved.Height);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), saved.Sha256);
            Assert.IsTrue(File.Exists(repository.MetadataPath));
            Assert.IsTrue(File.Exists(Path.Combine(repository.AssetDirectory, $"{saved.Id:N}.bin")));

            await using var content = await repository.OpenReadAsync(saved.Id);
            using var copy = new MemoryStream();
            await content!.CopyToAsync(copy);
            CollectionAssert.AreEqual(bytes, copy.ToArray());
        }
        finally { DeleteTempDirectory(directory); }
    }

    [TestMethod]
    public async Task DeleteAsync_ProtectsReferencedAssets_AndRemovesUnreferencedAssets()
    {
        var directory = CreateTempDirectory();
        try
        {
            var repository = CreateRepository(directory);
            var saved = await repository.SaveAsync(new ObsAsset
            {
                FileName = "sound.mp3",
                ContentType = "audio/mpeg",
                Kind = ObsAssetKind.Audio,
                ReferencedBy = ["sound-cue:classic"]
            }, new MemoryStream([1, 2, 3]));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => repository.DeleteAsync(saved.Id));
            var unreferenced = await repository.SaveAsync(new ObsAsset { FileName = "unused.bin" }, new MemoryStream([4, 5]));
            Assert.IsTrue(await repository.DeleteAsync(unreferenced.Id));
            Assert.IsNull(await repository.GetAsync(unreferenced.Id));
        }
        finally { DeleteTempDirectory(directory); }
    }

    [TestMethod]
    public async Task SaveAsync_AcceptsWebmVideoAssets()
    {
        var directory = CreateTempDirectory();
        try
        {
            var repository = CreateRepository(directory);
            var saved = await repository.SaveAsync(new ObsAsset
            {
                FileName = "ichiban.webm",
                ContentType = "video/webm",
                Kind = ObsAssetKind.Video
            }, new MemoryStream([1, 2, 3, 4]));

            Assert.AreEqual(ObsAssetKind.Video, saved.Kind);
            Assert.AreEqual("video/webm", saved.ContentType);
            Assert.IsNotNull(await repository.OpenReadAsync(saved.Id));
        }
        finally { DeleteTempDirectory(directory); }
    }

    [TestMethod]
    public async Task SaveAsync_RejectsNonWebmVideoAssets()
    {
        var directory = CreateTempDirectory();
        try
        {
            var repository = CreateRepository(directory);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => repository.SaveAsync(new ObsAsset
            {
                FileName = "animation.mp4",
                ContentType = "video/mp4",
                Kind = ObsAssetKind.Video
            }, new MemoryStream([1, 2, 3])));
        }
        finally { DeleteTempDirectory(directory); }
    }

    [TestMethod]
    public async Task ExportAndImportPackage_RoundTripsMetadataAndContent()
    {
        var sourceDirectory = CreateTempDirectory();
        var targetDirectory = CreateTempDirectory();
        try
        {
            var source = CreateRepository(sourceDirectory);
            var saved = await source.SaveAsync(new ObsAsset
            {
                FileName = "poster.png",
                ContentType = "image/png",
                Kind = ObsAssetKind.Image,
                ReferencedBy = ["template:7"]
            }, new MemoryStream(CreatePngHeader(64, 32)));

            await using var package = await source.ExportPackageAsync();
            var target = CreateRepository(targetDirectory);
            var imported = await target.ImportPackageAsync(package);

            Assert.AreEqual(1, imported.Count);
            Assert.AreEqual(saved.Id, imported[0].Id);
            Assert.AreEqual(saved.Sha256, imported[0].Sha256);
            CollectionAssert.AreEqual(CreatePngHeader(64, 32), await ReadBytesAsync(target, saved.Id));
            CollectionAssert.AreEqual(new[] { "template:7" }, imported[0].ReferencedBy);
        }
        finally
        {
            DeleteTempDirectory(sourceDirectory);
            DeleteTempDirectory(targetDirectory);
        }
    }

    [TestMethod]
    public async Task ImportPackage_RejectsChecksumMismatchWithoutChangingRepository()
    {
        var directory = CreateTempDirectory();
        try
        {
            var repository = CreateRepository(directory);
            var existing = await repository.SaveAsync(new ObsAsset { FileName = "existing.bin" }, new MemoryStream([1, 2, 3]));
            var importedId = Guid.NewGuid();
            using var package = new MemoryStream();
            using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                var manifest = new
                {
                    version = 1,
                    assets = new[]
                    {
                        new
                        {
                            asset = new ObsAsset { Id = importedId, FileName = "new.bin", Length = 3, Sha256 = "bad", ContentType = "application/octet-stream" },
                            entry = $"assets/{importedId:N}.bin"
                        }
                    }
                };
                await using (var manifestWriter = new StreamWriter(archive.CreateEntry("manifest.json").Open()))
                {
                    await manifestWriter.WriteAsync(JsonSerializer.Serialize(manifest));
                }
                await using var content = archive.CreateEntry($"assets/{importedId:N}.bin").Open();
                await content.WriteAsync(new byte[] { 4, 5, 6 });
            }
            package.Position = 0;

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => repository.ImportPackageAsync(package));
            Assert.AreEqual(1, (await repository.ListAsync()).Count);
            Assert.IsNotNull(await repository.GetAsync(existing.Id));
            Assert.IsNull(await repository.GetAsync(importedId));
        }
        finally { DeleteTempDirectory(directory); }
    }

    private static YamlObsAssetRepository CreateRepository(string directory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Directory"] = directory })
            .Build();
        return new YamlObsAssetRepository(configuration, new TestEnvironment(), new StorageGateProvider());
    }

    private static byte[] CreatePngHeader(int width, int height)
    {
        var bytes = new byte[32];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTempDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static async Task<byte[]> ReadBytesAsync(YamlObsAssetRepository repository, Guid id)
    {
        await using var content = await repository.OpenReadAsync(id);
        using var copy = new MemoryStream();
        await content!.CopyToAsync(copy);
        return copy.ToArray();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "EasyLotteryApiTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
