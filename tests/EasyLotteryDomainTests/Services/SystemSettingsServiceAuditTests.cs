using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryDomainTests.Helpers;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class SystemSettingsServiceAuditTests
    {
        [TestMethod]
        public async Task SaveSettingsAsync_WritesAuditRecord()
        {
            var store = new InMemoryEasyLotteryConfigStore();
            var auditService = new EasyLotteryAuditService(store);
            var service = new SystemSettingsService(store, auditService);

            await service.SaveSettingsAsync(new LotterySystemSettings
            {
                Audit = new AuditSettings
                {
                    ActorName = "管理員"
                },
                OpenAIKey = "secret"
            });

            var document = await store.LoadAsync();
            Assert.AreEqual(1, document.AuditRecords.Count);
            Assert.AreEqual("SystemSettings", document.AuditRecords[0].Category);
            Assert.AreEqual("管理員", document.AuditRecords[0].ChangedBy);
        }
    }
}
