using EasyLotteryDomain.Models.Overtime;
using EasyLotteryDomain.Services;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public sealed class OvertimeFeedStoreTests
    {
        [TestMethod]
        public async Task Add_NormalizesAndKeepsNewestFirst()
        {
            var store = new OvertimeFeedStore();

            var first = await store.AddAsync(new OvertimeSupportEvent
            {
                Source = OvertimeSupportSource.SuperChat,
                DisplayName = "  Alice  ",
                Message = "  hello  ",
                AmountDisplay = "  NT$100  "
            });

            var second = await store.AddAsync(new OvertimeSupportEvent
            {
                Source = OvertimeSupportSource.EcpayDonate,
                DisplayName = "Bob"
            });

            var snapshot = await store.SnapshotAsync();

            Assert.AreEqual(2, snapshot.Count);
            Assert.AreEqual(second.Id, snapshot[0].Id);
            Assert.AreEqual("Alice", first.DisplayName);
            Assert.AreEqual("hello", first.Message);
            Assert.AreEqual("NT$100", first.AmountDisplay);
        }

        [TestMethod]
        public async Task Clear_RemovesAllEvents()
        {
            var store = new OvertimeFeedStore();

            await store.AddAsync(new OvertimeSupportEvent { Source = OvertimeSupportSource.SuperChat, DisplayName = "A" });
            await store.ClearAsync();

            Assert.AreEqual(0, (await store.SnapshotAsync()).Count);
        }
    }
}
