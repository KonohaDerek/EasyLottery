using EasyLotteryDomain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class YouTubeServiceHelperTest
    {

        private string apiKey = "AIzaSyBvbITtuVbllyGzGATRLpw7XMdGnA7zOrI";


        //[TestMethod]
        //public async Task TestListChannelMembersAsync()
        //{
        //    var myConfiguration = new Dictionary<string, string>
        //    {
        //        {"YouTubeApi:CredentialsBase64", "eyJ3ZWIiOnsiY2xpZW50X2lkIjoiMzkyNDcxMjgxNDY5LTNwdWhxNDVhbDlqZDFjMDE1a3M1MzVicTRxbWw2aDg5LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwicHJvamVjdF9pZCI6ImRlcmVrcHJvamVjdC0zZTZmOSIsImF1dGhfdXJpIjoiaHR0cHM6Ly9hY2NvdW50cy5nb29nbGUuY29tL28vb2F1dGgyL2F1dGgiLCJ0b2tlbl91cmkiOiJodHRwczovL29hdXRoMi5nb29nbGVhcGlzLmNvbS90b2tlbiIsImF1dGhfcHJvdmlkZXJfeDUwOV9jZXJ0X3VybCI6Imh0dHBzOi8vd3d3Lmdvb2dsZWFwaXMuY29tL29hdXRoMi92MS9jZXJ0cyIsImNsaWVudF9zZWNyZXQiOiJHT0NTUFgtMnJfblAwNi1hMkJPVlU0WmxXRnJSWGRrXzNOaSJ9fQ=="},
        //    };

        //    var configuration = new ConfigurationBuilder()
        //        .AddInMemoryCollection(myConfiguration!)
        //        .Build();
      
        //    var svc = new YouTubeServiceHelper(configuration,"YoutubeDemo", "./Cred");
        //    var members = await svc.ListChannelMembersAsync();

        //    Assert.IsNotNull(members);
        //}

        [TestMethod]
        public void TestGetVideoIdAsync()
        {
            var url = "https://www.youtube.com/watch?v=-L9cBf3oAO0";

            var actual = YouTubeServiceHelper.GetYouTubeLiveID(url);

            Assert.AreEqual("-L9cBf3oAO0", actual);
        }

        [TestMethod]
        public void TestGetVideoIdWithLiveAsync()
        {
            var url = "https://www.youtube.com/live/M4_eD9CLTaI";

            var actual = YouTubeServiceHelper.GetYouTubeLiveID(url);

            Assert.AreEqual("M4_eD9CLTaI", actual);
        }

        [TestMethod]
        public async Task TestGetYoutubeLiveInfoAsync()
        {
            var myConfiguration = new Dictionary<string, string>
            {
                {"YouTubeApi:ApiKey",apiKey},
             {"YouTubeApi:CredentialsBase64", "eyJ3ZWIiOnsiY2xpZW50X2lkIjoiMzkyNDcxMjgxNDY5LTNwdWhxNDVhbDlqZDFjMDE1a3M1MzVicTRxbWw2aDg5LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwicHJvamVjdF9pZCI6ImRlcmVrcHJvamVjdC0zZTZmOSIsImF1dGhfdXJpIjoiaHR0cHM6Ly9hY2NvdW50cy5nb29nbGUuY29tL28vb2F1dGgyL2F1dGgiLCJ0b2tlbl91cmkiOiJodHRwczovL29hdXRoMi5nb29nbGVhcGlzLmNvbS90b2tlbiIsImF1dGhfcHJvdmlkZXJfeDUwOV9jZXJ0X3VybCI6Imh0dHBzOi8vd3d3Lmdvb2dsZWFwaXMuY29tL29hdXRoMi92MS9jZXJ0cyIsImNsaWVudF9zZWNyZXQiOiJHT0NTUFgtMnJfblAwNi1hMkJPVlU0WmxXRnJSWGRrXzNOaSJ9fQ=="},
            };
            var configuration = new ConfigurationBuilder()
               .AddInMemoryCollection(myConfiguration!)
               .Build();
            var logger = new Logger<YouTubeServiceHelper>(new NullLoggerFactory());
            //var liveID = "PytNXM6-Q4g";
            var liveID = "zrSJ7p5m0Bk";
            var svc = new YouTubeServiceHelper(configuration, logger);
            var actual = await svc.GetYoutubeLiveInfoAsync(liveID);

            Assert.IsNotNull(actual);
            Assert.AreEqual("Cg0KCy1MOWNCZjNvQU8wKicKGFVDa0VMTU1CZHk0Z1BqNm9vUXdZSi15ZxILLUw5Y0JmM29BTzA", actual.ActiveLiveChatId);

        }

        [TestMethod]
        public async Task TestListLiveChatMessageAsync()
        {
            var myConfiguration = new Dictionary<string, string>
            {
                {"YouTubeApi:ApiKey", apiKey},
                 {"YouTubeApi:CredentialsBase64", "eyJ3ZWIiOnsiY2xpZW50X2lkIjoiMzkyNDcxMjgxNDY5LTNwdWhxNDVhbDlqZDFjMDE1a3M1MzVicTRxbWw2aDg5LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwicHJvamVjdF9pZCI6ImRlcmVrcHJvamVjdC0zZTZmOSIsImF1dGhfdXJpIjoiaHR0cHM6Ly9hY2NvdW50cy5nb29nbGUuY29tL28vb2F1dGgyL2F1dGgiLCJ0b2tlbl91cmkiOiJodHRwczovL29hdXRoMi5nb29nbGVhcGlzLmNvbS90b2tlbiIsImF1dGhfcHJvdmlkZXJfeDUwOV9jZXJ0X3VybCI6Imh0dHBzOi8vd3d3Lmdvb2dsZWFwaXMuY29tL29hdXRoMi92MS9jZXJ0cyIsImNsaWVudF9zZWNyZXQiOiJHT0NTUFgtMnJfblAwNi1hMkJPVlU0WmxXRnJSWGRrXzNOaSJ9fQ=="},
            };
            var configuration = new ConfigurationBuilder()
             .AddInMemoryCollection(myConfiguration!)
             .Build();
            var logger = new Logger<YouTubeServiceHelper>(new NullLoggerFactory());
            var chatID = "Cg0KCy1MOWNCZjNvQU8wKicKGFVDa0VMTU1CZHk0Z1BqNm9vUXdZSi15ZxILLUw5Y0JmM29BTzA";

            var svc = new YouTubeServiceHelper(configuration, logger);
            var actual = await svc.ListLiveChatMessageAsync(chatID);

            Assert.IsNotNull(actual);
            Assert.IsTrue(actual.Any());
        }

        [TestMethod]
        public async Task TestListenLiveChatMessagesByGrpcAsync()
        {
            var chatID = "Cg0KC3pyU0o3cDVtMEJrKicKGFVDMk02MVlLNG50dDlpSy0yM1hoRHdjdxILenJTSjdwNW0wQms";
            var myConfiguration = new Dictionary<string, string>
            {
                {"YouTubeApi:ApiKey", apiKey},
                 {"YouTubeApi:CredentialsBase64", "eyJ3ZWIiOnsiY2xpZW50X2lkIjoiMzkyNDcxMjgxNDY5LTNwdWhxNDVhbDlqZDFjMDE1a3M1MzVicTRxbWw2aDg5LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwicHJvamVjdF9pZCI6ImRlcmVrcHJvamVjdC0zZTZmOSIsImF1dGhfdXJpIjoiaHR0cHM6Ly9hY2NvdW50cy5nb29nbGUuY29tL28vb2F1dGgyL2F1dGgiLCJ0b2tlbl91cmkiOiJodHRwczovL29hdXRoMi5nb29nbGVhcGlzLmNvbS90b2tlbiIsImF1dGhfcHJvdmlkZXJfeDUwOV9jZXJ0X3VybCI6Imh0dHBzOi8vd3d3Lmdvb2dsZWFwaXMuY29tL29hdXRoMi92MS9jZXJ0cyIsImNsaWVudF9zZWNyZXQiOiJHT0NTUFgtMnJfblAwNi1hMkJPVlU0WmxXRnJSWGRrXzNOaSJ9fQ=="},
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(myConfiguration!)
                .Build();
            var fac = LoggerFactory.Create((builder) =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                    builder.AddConsole();
            });
            var logger = new Logger<YouTubeServiceHelper>(fac);
            var svc = new YouTubeServiceHelper(configuration, logger);
            var cancellationToken = new CancellationTokenSource();
            var reader = svc.ListenLiveChatMessagesByGrpcAsync(chatID, cancellationToken.Token);
            await foreach (var msg in reader.ReadAllAsync(cancellationToken.Token))
            {
                Console.WriteLine($"{msg.UserName}:{msg.MessageText}");
                if (msg.MessageText.Contains("exit"))
                {
                    cancellationToken.Cancel();
                }
            }
            Assert.IsTrue(true);
        }
        
        [TestMethod]
        public async Task TestListLiveChatMessageWithAccessTokenAsync()
        {
            var myConfiguration = new Dictionary<string, string>
            {
                {"YouTubeApi:ApiKey",apiKey},
            };
            var configuration = new ConfigurationBuilder()
             .AddInMemoryCollection(myConfiguration!)
             .Build();
            var logger = new Logger<YouTubeServiceHelper>(new NullLoggerFactory());
            var chatID = "Cg0KC0hMWUVqcHBnMEZvKicKGFVDVzE3ZFBpNFpvVVZmR1N0MnlrNkR2dxILSExZRWpwcGcwRm8";

            var svc = new YouTubeServiceHelper(configuration, logger);
            var actual = await svc.ListLiveChatMessageAsync(chatID);
            Assert.IsNotNull(actual);
            Assert.IsTrue(actual.Any());
        }
    }
}