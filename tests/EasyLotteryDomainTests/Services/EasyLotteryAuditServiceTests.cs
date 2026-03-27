using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryDomainTests.Helpers;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class EasyLotteryAuditServiceTests
    {
        [TestMethod]
        public async Task RecordAsync_AppendsAuditRecordWithConfiguredActor()
        {
            var store = new InMemoryEasyLotteryConfigStore();
            await store.SaveAsync(new EasyLotteryConfigDocument
            {
                SystemSettings = new LotterySystemSettings
                {
                    Audit = new AuditSettings
                    {
                        ActorName = "主播 A"
                    }
                }
            });

            var service = new EasyLotteryAuditService(store);

            await service.RecordAsync("Template", "更新模板", "經典模板", "測試內容");

            var document = await store.LoadAsync();
            Assert.AreEqual(1, document.AuditRecords.Count);
            var record = document.AuditRecords[0];
            Assert.AreEqual("主播 A", record.ChangedBy);
            Assert.AreEqual("Template", record.Category);
            Assert.AreEqual("更新模板", record.Action);
            Assert.AreEqual("經典模板", record.TargetName);
            Assert.AreEqual("測試內容", record.Details);
        }

        [TestMethod]
        public async Task RecordAsync_TrimsAuditRecordsToRecentLimit()
        {
            var store = new InMemoryEasyLotteryConfigStore();
            var service = new EasyLotteryAuditService(store);

            for (var index = 0; index < 205; index++)
            {
                await service.RecordAsync("Settings", $"動作 {index}", "系統設定", $"內容 {index}", changedBy: "系統");
            }

            var document = await store.LoadAsync();
            Assert.AreEqual(200, document.AuditRecords.Count);
            Assert.AreEqual("動作 204", document.AuditRecords.Last().Action);
        }
    }
}
