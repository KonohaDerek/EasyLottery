using System.Buffers.Binary;
using System.Security.Cryptography;
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

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "EasyLotteryApiTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
