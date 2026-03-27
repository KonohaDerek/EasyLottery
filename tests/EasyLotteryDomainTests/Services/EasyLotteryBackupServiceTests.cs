using System.Text.Json;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryDomainTests.Helpers;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class EasyLotteryBackupServiceTests
    {
        [TestMethod]
        public async Task CreateBackupAsync_ReturnsVersionedPackageWithDocument()
        {
            var store = new InMemoryEasyLotteryConfigStore();
            await store.SaveAsync(new EasyLotteryConfigDocument
            {
                ConfigVersion = 7,
                SystemSettings = new LotterySystemSettings
                {
                    OpenAIKey = "test-key"
                },
                DrawingRules = new DrawingRuleSettings
                {
                    LevelRates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["VIP"] = 5
                    }
                }
            });

            var service = new EasyLotteryBackupService(store);

            var package = await service.CreateBackupAsync();

            Assert.AreEqual(1, package.BackupVersion);
            Assert.IsNotNull(package.Document);
            Assert.AreEqual(7, package.Document.ConfigVersion);
            Assert.AreEqual("test-key", package.Document.SystemSettings.OpenAIKey);
            Assert.AreEqual(5, package.Document.DrawingRules.LevelRates["VIP"]);
            Assert.AreNotEqual(default, package.CreatedAtUtc);
        }

        [TestMethod]
        public async Task RestoreAsync_RestoresVersionedBackupPackage()
        {
            var store = new InMemoryEasyLotteryConfigStore();
            var service = new EasyLotteryBackupService(store);
            var package = new EasyLotteryBackupPackage
            {
                BackupVersion = 1,
                CreatedAtUtc = new DateTime(2026, 3, 27, 1, 2, 3, DateTimeKind.Utc),
                Document = new EasyLotteryConfigDocument
                {
                    ConfigVersion = 9,
                    DrawingRulePresets = new List<DrawingRulePreset>
                    {
                        new()
                        {
                            Name = "Night Mode",
                            Description = "黑夜版本",
                            LevelRates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["A"] = 2,
                                ["B"] = 3
                            }
                        }
                    },
                    ActivityResults = new List<ActivityResultRecord>
                    {
                        new()
                        {
                            Id = 99,
                            ActivityType = ActivityResultType.PokeBox,
                            ActivityName = "活動結果"
                        }
                    }
                }
            };

            var json = service.SerializeBackup(package);

            await service.RestoreAsync(json);

            var restored = await store.LoadAsync();
            Assert.AreEqual(9, restored.ConfigVersion);
            Assert.AreEqual(1, restored.DrawingRulePresets.Count);
            Assert.AreEqual("Night Mode", restored.DrawingRulePresets[0].Name);
            Assert.AreEqual(2, restored.DrawingRulePresets[0].LevelRates["A"]);
            Assert.AreEqual(1, restored.ActivityResults.Count);
            Assert.AreEqual(99, restored.ActivityResults[0].Id);
        }

        [TestMethod]
        public async Task RestoreAsync_AcceptsLegacyDocumentJson()
        {
            var store = new InMemoryEasyLotteryConfigStore();
            var service = new EasyLotteryBackupService(store);
            var document = new EasyLotteryConfigDocument
            {
                ConfigVersion = 3,
                DrawingRules = new DrawingRuleSettings
                {
                    LevelRates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["VIP"] = 8
                    }
                }
            };

            var json = JsonSerializer.Serialize(document, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            });

            await service.RestoreAsync(json);

            var restored = await store.LoadAsync();
            Assert.AreEqual(3, restored.ConfigVersion);
            Assert.AreEqual(8, restored.DrawingRules.LevelRates["VIP"]);
        }
    }
}
