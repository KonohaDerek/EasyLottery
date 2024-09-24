using EasyLotteryDomain.Services;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class YouTubeServiceHelperTest
    {

        private string apiKey = "AIzaSyBvbITtuVbllyGzGATRLpw7XMdGnA7zOrI";


        [TestMethod]
        public async Task TestListChannelMembersAsync()
        {
            var myConfiguration = new Dictionary<string, string>
            {
                {"YouTubeApi:CredentialsBase64", "eyJ3ZWIiOnsiY2xpZW50X2lkIjoiMzkyNDcxMjgxNDY5LTNwdWhxNDVhbDlqZDFjMDE1a3M1MzVicTRxbWw2aDg5LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwicHJvamVjdF9pZCI6ImRlcmVrcHJvamVjdC0zZTZmOSIsImF1dGhfdXJpIjoiaHR0cHM6Ly9hY2NvdW50cy5nb29nbGUuY29tL28vb2F1dGgyL2F1dGgiLCJ0b2tlbl91cmkiOiJodHRwczovL29hdXRoMi5nb29nbGVhcGlzLmNvbS90b2tlbiIsImF1dGhfcHJvdmlkZXJfeDUwOV9jZXJ0X3VybCI6Imh0dHBzOi8vd3d3Lmdvb2dsZWFwaXMuY29tL29hdXRoMi92MS9jZXJ0cyIsImNsaWVudF9zZWNyZXQiOiJHT0NTUFgtMnJfblAwNi1hMkJPVlU0WmxXRnJSWGRrXzNOaSJ9fQ=="},
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(myConfiguration!)
                .Build();
      
            var svc = new YouTubeServiceHelper(configuration,"YoutubeDemo", "./Cred");
            var members = await svc.ListChannelMembersAsync();

            Assert.IsNotNull(members);
        }

        [TestMethod]
        public void TestGetVideoIdAsync()
        {
            var url = "https://www.youtube.com/watch?v=-L9cBf3oAO0";

            var actual = YouTubeServiceHelper.GetYouTubeLiveID(url);

            Assert.AreEqual("-L9cBf3oAO0", actual);
        }

        [TestMethod]
        public async Task TestGetYoutubeLiveInfoAsync()
        {
            var liveID = "-L9cBf3oAO0";
            var svc = new YouTubeServiceHelper(new ConfigurationBuilder().Build(), apiKey);
            var actual = await svc.GetYoutubeLiveInfoAsync(liveID);

            Assert.IsNotNull(actual);
            Assert.AreEqual("Cg0KCy1MOWNCZjNvQU8wKicKGFVDa0VMTU1CZHk0Z1BqNm9vUXdZSi15ZxILLUw5Y0JmM29BTzA", actual.ActiveLiveChatId);

        }

        [TestMethod]
        public async Task TestListLiveChatMessageAsync()
        {
            var chatID = "Cg0KCy1MOWNCZjNvQU8wKicKGFVDa0VMTU1CZHk0Z1BqNm9vUXdZSi15ZxILLUw5Y0JmM29BTzA";

            var svc = new YouTubeServiceHelper(new ConfigurationBuilder().Build(), apiKey);
            var actual = await svc.ListLiveChatMessageAsync(chatID);

            Assert.IsNotNull(actual);
            Assert.IsTrue(actual.Any());
            

        }
    }
}