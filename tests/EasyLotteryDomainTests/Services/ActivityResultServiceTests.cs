using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;
using EasyLotteryDomainTests.Helpers;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class ActivityResultServiceTests
    {
        [TestMethod]
        public async Task RecordPokeActivityAsync_SavesRevealedCellsWithMetadata()
        {
            var store = new InMemoryEasyLotteryConfigStore();
            var firstReveal = new DateTime(2026, 3, 12, 1, 0, 0, DateTimeKind.Utc);
            var secondReveal = firstReveal.AddMinutes(3);

            await store.SaveAsync(new EasyLotteryConfigDocument
            {
                IdSequence = new LotteryIdSequence { NextActivityResultId = 1 },
                PokeTemplates =
                [
                    new PokeTemplate
                    {
                        Id = 7,
                        Name = "三月會員抽獎",
                        Cells =
                        [
                            new PokeCell { Id = 1, Index = 0, Title = "A 獎", SubTitle = "限量周邊", IsRevealed = true, RevealedAt = firstReveal, RevealedColor = "#ffcc00" },
                            new PokeCell { Id = 2, Index = 1, Title = "B 獎", SubTitle = "簽名板", IsRevealed = true, RevealedAt = secondReveal, RevealedColor = "#99ddff" },
                            new PokeCell { Id = 3, Index = 2, Title = "C 獎", IsRevealed = false }
                        ]
                    }
                ]
            });

            var service = new ActivityResultService(store);

            var record = await service.RecordPokeActivityAsync(7);
            var savedDocument = await store.LoadAsync();

            Assert.AreEqual(ActivityResultType.PokeBox, record.ActivityType);
            Assert.AreEqual("三月會員抽獎", record.ActivityName);
            Assert.AreEqual("已揭曉 2 / 3 格", record.Summary);
            Assert.AreEqual(2, record.Items.Count);
            Assert.AreEqual("A 獎", record.Items[0].Name);
            Assert.AreEqual("B 獎", record.Items[1].Name);
            Assert.AreEqual(1, savedDocument.ActivityResults.Count);
            Assert.AreEqual(2, savedDocument.IdSequence.NextActivityResultId);
        }

        [TestMethod]
        public async Task RecordRouletteActivityAsync_SavesWinningSegment()
        {
            var store = new InMemoryEasyLotteryConfigStore();

            await store.SaveAsync(new EasyLotteryConfigDocument
            {
                IdSequence = new LotteryIdSequence { NextActivityResultId = 4 },
                RouletteTemplates =
                [
                    new RouletteTemplate
                    {
                        Id = 3,
                        Name = "直播抽抽樂",
                        Segments =
                        [
                            new RouletteSegment { Id = 1, Index = 0, Title = "銘謝惠顧", Color = "#cccccc" },
                            new RouletteSegment { Id = 2, Index = 1, Title = "頭獎", Color = "#ff3366", ImageUrl = "https://example.com/prize.png" }
                        ]
                    }
                ]
            });

            var service = new ActivityResultService(store);

            var record = await service.RecordRouletteActivityAsync(3, new SpinResult
            {
                SegmentIndex = 1,
                SegmentTitle = "頭獎"
            });
            var savedDocument = await store.LoadAsync();

            Assert.AreEqual(ActivityResultType.Roulette, record.ActivityType);
            Assert.AreEqual("直播抽抽樂", record.ActivityName);
            Assert.AreEqual("中獎項目：頭獎", record.Summary);
            Assert.AreEqual(1, record.Items.Count);
            Assert.AreEqual("頭獎", record.Items[0].Name);
            Assert.AreEqual("#ff3366", record.Items[0].Color);
            Assert.AreEqual(1, savedDocument.ActivityResults.Count);
            Assert.AreEqual(5, savedDocument.IdSequence.NextActivityResultId);
        }

        [TestMethod]
        public void FilterActivityResults_FiltersBySearchTypeAndDateRange()
        {
            var records = new List<ActivityResultRecord>
            {
                new()
                {
                    Id = 1,
                    ActivityType = ActivityResultType.PokeBox,
                    ActivityName = "四月戳戳樂",
                    Summary = "已揭曉 3 / 9 格",
                    ActivityDateUtc = new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc),
                    Items =
                    [
                        new ActivityResultItem { Order = 1, Name = "A 獎", Description = "頭獎" }
                    ]
                },
                new()
                {
                    Id = 2,
                    ActivityType = ActivityResultType.Roulette,
                    ActivityName = "直播轉盤",
                    Summary = "中獎項目：頭獎",
                    ActivityDateUtc = new DateTime(2026, 4, 12, 10, 0, 0, DateTimeKind.Utc),
                    Items =
                    [
                        new ActivityResultItem { Order = 1, Name = "頭獎", Description = "第 2 格" }
                    ]
                }
            };

            var filtered = ActivityResultService.FilterActivityResults(
                records,
                searchText: "頭獎",
                activityType: ActivityResultType.Roulette,
                startDate: new DateOnly(2026, 4, 11),
                endDate: new DateOnly(2026, 4, 13));

            Assert.AreEqual(1, filtered.Count);
            Assert.AreEqual(2, filtered[0].Id);
        }

        [TestMethod]
        public void SerializeActivityResults_ReturnsIndentedJson()
        {
            var json = ActivityResultService.SerializeActivityResults(
                new[]
                {
                    new ActivityResultRecord
                    {
                        Id = 1,
                        ActivityType = ActivityResultType.Roulette,
                        ActivityName = "直播轉盤",
                        Summary = "中獎項目：頭獎"
                    }
                });

            Assert.IsTrue(json.Contains("\"ActivityName\""));
            Assert.IsTrue(json.Contains("\n"));
        }
    }
}
