using EasyLotteryApi;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using EasyLotteryApplication.Settings;
using EasyLotteryInfrastructure.Storage;
using EasyLotteryInfrastructure.Settings;
using YamlDotNet.Serialization;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SettingsFileStoreTests
{
    private static readonly IDeserializer Deserializer = YamlSerialization.CreateDeserializerBuilder().Build();

    [TestMethod]
    public async Task Constructor_CreatesSplitYamlDocuments()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);

            Assert.IsTrue(File.Exists(store.ConfigPath));
            Assert.IsTrue(File.Exists(store.ActivitiesPath));
            Assert.IsTrue(File.Exists(store.ActivityResultsPath));

            var settings = Deserialize<SettingsYamlDocument>(await File.ReadAllTextAsync(store.ConfigPath));
            var activities = Deserialize<ActivitiesYamlDocument>(await File.ReadAllTextAsync(store.ActivitiesPath));
            var results = Deserialize<ActivityResultsYamlDocument>(await File.ReadAllTextAsync(store.ActivityResultsPath));

            Assert.IsNotNull(settings.SystemSettings);
            Assert.AreEqual(0, activities.DonateLotteryActivities.Count);
            Assert.AreEqual(0, results.ActivityResults.Count);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public async Task Constructor_LeavesExistingSplitFiles_Intact()
    {
        var directory = CreateTempDirectory();
        try
        {
            var start = new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);
            var activitiesDocument = new ActivitiesYamlDocument
            {
                DonateLotteryActivities =
                [
                    new DonateLotteryActivity
                    {
                        Id = 4,
                        Name = "一番賞",
                        StartsAtUtc = start,
                        EndsAtUtc = end
                    }
                ]
            };
            var resultsDocument = new ActivityResultsYamlDocument
            {
                ActivityResults =
                [
                    new ActivityResultRecord
                    {
                        Id = 9,
                        ActivityName = "一番賞",
                        ActivityDateUtc = start.UtcDateTime
                    }
                ],
                ProcessedDonatePaymentIds = ["payment-1"]
            };
            await File.WriteAllTextAsync(Path.Combine(directory, "activities.yaml"), Serialize(activitiesDocument));
            await File.WriteAllTextAsync(Path.Combine(directory, "activity-results.yaml"), Serialize(resultsDocument));

            var store = CreateStore(directory);
            var merged = await store.ReadAsync(CancellationToken.None);

            Assert.AreEqual(start, merged.DonateLotteryActivities[0].StartsAtUtc);
            Assert.AreEqual(end, merged.DonateLotteryActivities[0].EndsAtUtc);
            Assert.AreEqual(9, merged.ActivityResults[0].Id);
            CollectionAssert.Contains(merged.ProcessedDonatePaymentIds, "payment-1");

            var activities = Deserialize<ActivitiesYamlDocument>(await File.ReadAllTextAsync(store.ActivitiesPath));
            var results = Deserialize<ActivityResultsYamlDocument>(await File.ReadAllTextAsync(store.ActivityResultsPath));

            Assert.AreEqual("一番賞", activities.DonateLotteryActivities[0].Name);
            Assert.AreEqual(1, results.ProcessedDonatePaymentIds.Count);
            Assert.AreEqual("payment-1", results.ProcessedDonatePaymentIds[0]);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public async Task UpdateAsync_WritesChangesToTheSplitFiles()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);

            await store.UpdateAsync(document =>
            {
                document.SystemSettings.ResultNotificationEmail = "results@example.test";
                document.DonateLotteryActivities.Add(new DonateLotteryActivity
                {
                    Id = 1,
                    Name = "測試活動",
                    StartsAtUtc = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                    EndsAtUtc = new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero)
                });
                document.ProcessedDonatePaymentIds.Add("payment-2");
                return true;
            }, CancellationToken.None);

            var settings = Deserialize<SettingsYamlDocument>(await File.ReadAllTextAsync(store.ConfigPath));
            var activities = Deserialize<ActivitiesYamlDocument>(await File.ReadAllTextAsync(store.ActivitiesPath));
            var results = Deserialize<ActivityResultsYamlDocument>(await File.ReadAllTextAsync(store.ActivityResultsPath));

            Assert.AreEqual("results@example.test", settings.SystemSettings.ResultNotificationEmail);
            Assert.AreEqual("測試活動", activities.DonateLotteryActivities[0].Name);
            CollectionAssert.Contains(results.ProcessedDonatePaymentIds, "payment-2");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static SettingsFileStore CreateStore(string directory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Directory"] = directory })
            .Build();
        var environment = new TestEnvironment();
        var storageGates = new StorageGateProvider();
        return new SettingsFileStore(
            new ConfigSecretRedactor(),
            new YamlSettingsDocumentRepository(configuration, environment, storageGates),
            new YamlActivitiesDocumentRepository(configuration, environment, storageGates),
            new YamlActivityResultsDocumentRepository(configuration, environment, storageGates));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTempDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Serialize<T>(T document) where T : class =>
        YamlSerialization.CreateSerializerBuilder()
            .Build()
            .Serialize(document);

    private static T Deserialize<T>(string yaml) where T : class =>
        Deserializer.Deserialize<T>(yaml) ?? throw new InvalidOperationException($"Unable to deserialize {typeof(T).Name}.");

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "EasyLotteryApiTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
