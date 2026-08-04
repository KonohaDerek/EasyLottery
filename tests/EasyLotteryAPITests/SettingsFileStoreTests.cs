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
using EasyLotteryInfrastructure.DonateActivities;
using Microsoft.Extensions.Logging.Abstractions;
using EasyLotteryApplication.DonateActivities;

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

    [TestMethod]
    public async Task BrowserSnapshot_RejectsStaleETag()
    {
        var directory = CreateTempDirectory();
        try
        {
            var first = CreateStore(directory);
            var second = CreateStore(directory);
            var snapshot = await first.ReadForBrowserSnapshotAsync(CancellationToken.None);

            await second.UpdateAsync(document =>
            {
                document.SystemSettings.ResultNotificationEmail = "changed@example.test";
                return true;
            }, CancellationToken.None);

            await Assert.ThrowsExactlyAsync<ConfigurationConcurrencyException>(() =>
                first.SaveBrowserUpdateAsync(snapshot.Yaml, snapshot.ETag, CancellationToken.None));
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public async Task ReadAsync_MigratesVersionOneDocumentsAndPersistsDefaults()
    {
        var directory = CreateTempDirectory();
        try
        {
            var activities = new ActivitiesYamlDocument
            {
                ConfigVersion = 1,
                DonateLotteryActivities =
                [
                    new DonateLotteryActivity { Id = 7, Name = "舊活動", StartsAtUtc = DateTimeOffset.UtcNow.AddHours(-1), EndsAtUtc = DateTimeOffset.UtcNow.AddHours(1) }
                ]
            };
            await File.WriteAllTextAsync(Path.Combine(directory, "activities.yaml"), Serialize(activities));
            var store = CreateStore(directory);

            var document = await store.ReadAsync(CancellationToken.None);
            Assert.AreNotEqual(Guid.Empty, document.DonateLotteryActivities[0].PublicId);
            Assert.AreEqual(2, Deserialize<ActivitiesYamlDocument>(await File.ReadAllTextAsync(store.ActivitiesPath)).ConfigVersion);
            Assert.AreEqual("classic", document.DonateLotteryActivities[0].PolaroidTemplateKey);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public async Task ReadAsync_QuarantinesCorruptYamlInsteadOfReturningDefaults()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.yaml");
            await File.WriteAllTextAsync(path, "ConfigVersion: [broken");
            var store = CreateStore(directory);

            await Assert.ThrowsExactlyAsync<YamlStorageException>(() => store.ReadAsync(CancellationToken.None));
            Assert.IsFalse(File.Exists(path));
            Assert.IsTrue(Directory.EnumerateFiles(directory, "settings.yaml.corrupt.*").Any());
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public async Task BackupSet_CanRestorePreviousCompleteConfiguration()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            await store.UpdateAsync(document =>
            {
                document.SystemSettings.ResultNotificationEmail = "before@example.test";
                return true;
            }, CancellationToken.None);
            await store.UpdateAsync(document =>
            {
                document.SystemSettings.ResultNotificationEmail = "after@example.test";
                return true;
            }, CancellationToken.None);

            var backup = store.ListBackups().First();
            await store.RestoreBackupAsync(backup.Id, CancellationToken.None);

            var restored = await store.ReadAsync(CancellationToken.None);
            Assert.AreEqual("before@example.test", restored.SystemSettings.ResultNotificationEmail);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public async Task DonateEventStore_QuarantinesInvalidLinesAndKeepsValidEvents()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Directory"] = directory })
                .Build();
            var environment = new TestEnvironment();
            var eventStore = new YamlDonateActivityEventStore(configuration, environment, new StorageGateProvider(), NullLogger<YamlDonateActivityEventStore>.Instance);
            await File.WriteAllTextAsync(eventStore.EventLogPath, "{\"type\":\"Saved\",\"activityId\":1}\nnot-json\n");

            var events = await eventStore.ReadAllAsync(CancellationToken.None);

            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(Directory.EnumerateFiles(directory, "donate-activity-events.jsonl.bad.*").Any());
            Assert.IsFalse((await File.ReadAllTextAsync(eventStore.EventLogPath)).Contains("not-json", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public async Task ReadAsync_RecoversPreviousVersionWhenTransactionMarkerRemains()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            await store.UpdateAsync(document =>
            {
                document.SystemSettings.ResultNotificationEmail = "stable@example.test";
                return true;
            }, CancellationToken.None);
            var backup = store.ListBackups().First();
            var activitiesPath = store.ActivitiesPath;
            await File.WriteAllTextAsync(activitiesPath, "DonateLotteryActivities: []\nConfigVersion: 2\n");
            var markerDirectory = Path.Combine(directory, "config-backups");
            await File.WriteAllTextAsync(Path.Combine(markerDirectory, "pending-transaction"), backup.Id);

            var recovered = await store.ReadAsync(CancellationToken.None);

            Assert.AreEqual("", recovered.SystemSettings.ResultNotificationEmail);
            Assert.IsFalse(File.Exists(Path.Combine(markerDirectory, "pending-transaction")));
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
            configuration,
            new ConfigSecretRedactor(),
            new YamlSettingsDocumentRepository(configuration, environment, storageGates),
            new YamlActivitiesDocumentRepository(configuration, environment, storageGates),
            new YamlActivityResultsDocumentRepository(configuration, environment, storageGates),
            storageGates);
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
