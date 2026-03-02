using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryDomainTests.Models
{
    [TestClass]
    public class SystemSettingTest
    {
        [TestMethod]
        public void DefaultValues_AreEmptyStrings()
        {
            var setting = new SystemSetting();
            Assert.AreEqual(0, setting.Id);
            Assert.AreEqual("", setting.YoutubeApiKey);
            Assert.AreEqual("", setting.YoutubeCredentialsJson);
            Assert.AreEqual("", setting.OpenAIKey);
        }

        [TestMethod]
        public void SetProperties_ValuesAreStored()
        {
            var setting = new SystemSetting
            {
                Id = 1,
                YoutubeApiKey = "AIza-test-key",
                YoutubeCredentialsJson = "{\"client_id\":\"test\"}",
                OpenAIKey = "sk-test-key"
            };

            Assert.AreEqual(1, setting.Id);
            Assert.AreEqual("AIza-test-key", setting.YoutubeApiKey);
            Assert.AreEqual("{\"client_id\":\"test\"}", setting.YoutubeCredentialsJson);
            Assert.AreEqual("sk-test-key", setting.OpenAIKey);
        }

        [TestMethod]
        public void UpdateProperty_ValueChanges()
        {
            var setting = new SystemSetting { YoutubeApiKey = "old-key" };
            Assert.AreEqual("old-key", setting.YoutubeApiKey);

            setting.YoutubeApiKey = "new-key";
            Assert.AreEqual("new-key", setting.YoutubeApiKey);
        }
    }
}
