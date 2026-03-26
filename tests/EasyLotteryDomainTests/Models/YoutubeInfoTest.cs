using EasyLotteryDomain.Models.Youtube;

namespace EasyLotteryDomainTests.Models
{
    [TestClass]
    public class YoutubeInfoTest
    {
        [TestMethod]
        public void DefaultValues_AreEmptyStrings()
        {
            var info = new YoutubeInfo();
            Assert.AreEqual(string.Empty, info.ChannelId);
            Assert.AreEqual(string.Empty, info.ChannelTitle);
            Assert.AreEqual(string.Empty, info.ChannelDescription);
        }

        [TestMethod]
        public void SetProperties_ValuesAreStored()
        {
            var info = new YoutubeInfo
            {
                ChannelId = "UC12345",
                ChannelTitle = "測試頻道",
                ChannelDescription = "這是一個測試頻道"
            };

            Assert.AreEqual("UC12345", info.ChannelId);
            Assert.AreEqual("測試頻道", info.ChannelTitle);
            Assert.AreEqual("這是一個測試頻道", info.ChannelDescription);
        }
    }
}
