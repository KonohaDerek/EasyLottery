using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryDomainTests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class YouTubeServiceHelperUnitTest
    {
        // 用於建立 YouTubeServiceHelper 的共用 Base64 憑證（測試用）
        private const string TestCredentialsBase64 = "eyJ3ZWIiOnsiY2xpZW50X2lkIjoiMzkyNDcxMjgxNDY5LTNwdWhxNDVhbDlqZDFjMDE1a3M1MzVicTRxbWw2aDg5LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwicHJvamVjdF9pZCI6ImRlcmVrcHJvamVjdC0zZTZmOSIsImF1dGhfdXJpIjoiaHR0cHM6Ly9hY2NvdW50cy5nb29nbGUuY29tL28vb2F1dGgyL2F1dGgiLCJ0b2tlbl91cmkiOiJodHRwczovL29hdXRoMi5nb29nbGVhcGlzLmNvbS90b2tlbiIsImF1dGhfcHJvdmlkZXJfeDUwOV9jZXJ0X3VybCI6Imh0dHBzOi8vd3d3Lmdvb2dsZWFwaXMuY29tL29hdXRoMi92MS9jZXJ0cyIsImNsaWVudF9zZWNyZXQiOiJHT0NTUFgtMnJfblAwNi1hMkJPVlU0WmxXRnJSWGRrXzNOaSJ9fQ==";

        private YouTubeServiceHelper CreateService(Dictionary<string, string>? extraConfig = null)
        {
            var config = new Dictionary<string, string>
            {
                { "YouTubeApi:ApiKey", "test-api-key" },
                { "YouTubeApi:CredentialsBase64", TestCredentialsBase64 },
                { "YouTubeApi:RedirectUri", "https://localhost/callback" },
                { "YouTubeApi:RefreshToken", "" },
            };

            if (extraConfig != null)
            {
                foreach (var kv in extraConfig)
                    config[kv.Key] = kv.Value;
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(config!)
                .Build();

            var logger = new Logger<YouTubeServiceHelper>(new NullLoggerFactory());
            return new YouTubeServiceHelper(configuration, logger);
        }

        private YouTubeServiceHelper CreateServiceWithSettingsStore(
            Dictionary<string, string>? extraConfig = null,
            Action<EasyLotteryConfigDocument>? configureDocument = null)
        {
            var config = new Dictionary<string, string>
            {
                { "YouTubeApi:ApiKey", "test-api-key" },
                { "YouTubeApi:CredentialsBase64", TestCredentialsBase64 },
                { "YouTubeApi:RedirectUri", "https://localhost/callback" },
                { "YouTubeApi:RefreshToken", "" },
            };

            if (extraConfig != null)
            {
                foreach (var kv in extraConfig)
                {
                    config[kv.Key] = kv.Value;
                }
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(config!)
                .Build();

            var store = new InMemoryEasyLotteryConfigStore();
            var document = store.LoadAsync().GetAwaiter().GetResult();
            configureDocument?.Invoke(document);
            store.SaveAsync(document).GetAwaiter().GetResult();

            var logger = new Logger<YouTubeServiceHelper>(new NullLoggerFactory());
            var settingsService = new SystemSettingsService(store);
            return new YouTubeServiceHelper(settingsService, configuration, logger);
        }

        #region GetYouTubeLiveID Tests

        [TestMethod]
        public void GetYouTubeLiveID_WatchUrl_ReturnsVideoId()
        {
            var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("dQw4w9WgXcQ", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_LiveUrl_ReturnsVideoId()
        {
            var url = "https://www.youtube.com/live/M4_eD9CLTaI";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("M4_eD9CLTaI", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_ShortUrl_ReturnsVideoId()
        {
            var url = "https://youtu.be/dQw4w9WgXcQ";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("dQw4w9WgXcQ", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_EmbedUrl_ReturnsVideoId()
        {
            var url = "https://www.youtube.com/embed/dQw4w9WgXcQ";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("dQw4w9WgXcQ", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_WatchUrlWithExtraParams_ReturnsVideoId()
        {
            var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=120s&list=PLtest";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("dQw4w9WgXcQ", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_WatchUrlWithHyphen_ReturnsVideoId()
        {
            var url = "https://www.youtube.com/watch?v=-L9cBf3oAO0";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("-L9cBf3oAO0", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_InvalidUrl_ReturnsEmpty()
        {
            var url = "https://www.google.com";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_EmptyString_ReturnsEmpty()
        {
            var result = YouTubeServiceHelper.GetYouTubeLiveID("");
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_HttpWithoutS_ReturnsVideoId()
        {
            var url = "http://www.youtube.com/watch?v=dQw4w9WgXcQ";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("dQw4w9WgXcQ", result);
        }

        [TestMethod]
        public void GetYouTubeLiveID_NoWww_ReturnsVideoId()
        {
            var url = "https://youtube.com/watch?v=dQw4w9WgXcQ";
            var result = YouTubeServiceHelper.GetYouTubeLiveID(url);
            Assert.AreEqual("dQw4w9WgXcQ", result);
        }

        #endregion

        #region IsAuthorized & Logout Tests

        [TestMethod]
        public void IsAuthorized_Default_ReturnsFalse()
        {
            var svc = CreateService();
            Assert.IsFalse(svc.IsAuthorized);
        }

        [TestMethod]
        public void Logout_AfterLogout_IsAuthorizedFalse()
        {
            var svc = CreateService();
            // 預設就是未授權
            Assert.IsFalse(svc.IsAuthorized);

            // Logout 應不報錯，且狀態仍為 false
            svc.Logout();
            Assert.IsFalse(svc.IsAuthorized);
        }

        #endregion

        #region GetAuthorizeUrl Tests

        [TestMethod]
        public void GetAuthorizeUrl_WithoutRedirectUri_UsesConfigRedirectUri()
        {
            var svc = CreateService();
            var url = svc.GetAuthorizeUrl();

            Assert.IsTrue(url.StartsWith("https://accounts.google.com/o/oauth2/v2/auth"));
            Assert.IsTrue(url.Contains("response_type=code"));
            Assert.IsTrue(url.Contains("client_id="));
            Assert.IsTrue(url.Contains("redirect_uri="));
            Assert.IsTrue(url.Contains("scope="));
            Assert.IsTrue(url.Contains("access_type=offline"));
            Assert.IsTrue(url.Contains("prompt=consent"));
        }

        [TestMethod]
        public void GetAuthorizeUrl_WithCustomRedirectUri_UsesCustomUri()
        {
            var svc = CreateService();
            var customUri = "https://my-app.example.com/callback";
            var url = svc.GetAuthorizeUrl(customUri);

            Assert.IsTrue(url.Contains(Uri.EscapeDataString(customUri)));
        }

        [TestMethod]
        public void GetAuthorizeUrl_WithEmptyRedirectUri_FallsBackToConfig()
        {
            var svc = CreateService();
            var url = svc.GetAuthorizeUrl("");

            // 空字串應 fallback 到 config 的 RedirectUri
            Assert.IsTrue(url.Contains("redirect_uri="));
            Assert.IsTrue(url.Contains(Uri.EscapeDataString("https://localhost/callback")));
        }

        #endregion

        #region Constructor Tests

        [TestMethod]
        public void Constructor_MissingCredentials_ThrowsException()
        {
            var config = new Dictionary<string, string>
            {
                { "YouTubeApi:ApiKey", "test" },
                // CredentialsBase64 is missing
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(config!)
                .Build();

            var logger = new Logger<YouTubeServiceHelper>(new NullLoggerFactory());
            Assert.ThrowsExactly<Exception>(() => new YouTubeServiceHelper(configuration, logger));
        }

        [TestMethod]
        public void Constructor_ValidConfig_CreatesInstance()
        {
            var svc = CreateService();
            Assert.IsNotNull(svc);
        }

        #endregion

        #region RefreshTokenAsync Tests

        [TestMethod]
        public async Task RefreshTokenAsync_EmptyRefreshToken_ThrowsException()
        {
            var svc = CreateService();
            await Assert.ThrowsExactlyAsync<Exception>(async () => await svc.RefreshTokenAsync());
        }

        [TestMethod]
        public async Task HasConfiguredCredentialsAsync_WithBlankPersistedCredentials_UsesBundledAppSettings()
        {
            var svc = CreateServiceWithSettingsStore(configureDocument: document =>
            {
                document.SystemSettings.YouTube.CredentialsBase64 = "";
                document.SystemSettings.YouTube.RedirectUri = "";
            });

            var configured = await svc.HasConfiguredCredentialsAsync();

            Assert.IsTrue(configured);
        }

        [TestMethod]
        public async Task GetAuthorizeUrlAsync_WithBlankPersistedCredentials_UsesBundledRedirectUri()
        {
            var svc = CreateServiceWithSettingsStore(configureDocument: document =>
            {
                document.SystemSettings.YouTube.CredentialsBase64 = "";
                document.SystemSettings.YouTube.RedirectUri = "";
            });

            var url = await svc.GetAuthorizeUrlAsync();

            Assert.IsTrue(url.Contains(Uri.EscapeDataString("https://localhost/callback")));
        }

        [TestMethod]
        public async Task HasRefreshTokenAsync_WithPersistedRefreshToken_PrefersYamlToken()
        {
            var svc = CreateServiceWithSettingsStore(configureDocument: document =>
            {
                document.SystemSettings.YouTube.RefreshToken = "persisted-refresh-token";
            });

            var configured = await svc.HasRefreshTokenAsync();

            Assert.IsTrue(configured);
        }

        #endregion
    }
}
