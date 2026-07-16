using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Youtube;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Youtube.Api.V3;
using Grpc.Core;
using Grpc.Net.Client;
using System.Threading.Channels;

namespace EasyLotteryDomain.Services
{
    public class YouTubeServiceHelper
    {
        private readonly IConfiguration configuration;
        private readonly SystemSettingsService? systemSettingsService;

        private static readonly string[] Scopes = { YouTubeService.Scope.YoutubeReadonly, "https://www.googleapis.com/auth/youtube.channel-memberships.creator" };

        private const string authorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";


        private string accessToken="";

        /// <summary>
        /// 是否已完成 OAuth 授權
        /// </summary>
        public bool IsAuthorized => !string.IsNullOrWhiteSpace(accessToken);

        /// <summary>
        /// 授權狀態變更事件
        /// </summary>
        public event Action? AuthStateChanged;

        internal record YoutubeCredentials(string client_id, string client_secret, string RedirectUri);

        private readonly ILogger<YouTubeServiceHelper> logger;

        public YouTubeServiceHelper(SystemSettingsService systemSettingsService, IConfiguration configuration, ILogger<YouTubeServiceHelper> logger)
        {
            this.logger = logger;
            this.systemSettingsService = systemSettingsService;
            this.configuration = configuration;
        }

        public YouTubeServiceHelper(IConfiguration configuration, ILogger<YouTubeServiceHelper> logger)
        {
            this.logger = logger;
            this.configuration = configuration;
            _ = GetLegacyAuthConfiguration();
        }

        public async Task<bool> HasConfiguredCredentialsAsync()
        {
            var settings = await GetEffectiveYouTubeSettingsAsync();
            return !string.IsNullOrWhiteSpace(settings.ApiKey);
        }

        public async Task<bool> HasRefreshTokenAsync()
        {
            var settings = await GetEffectiveYouTubeSettingsAsync();
            return !string.IsNullOrWhiteSpace(settings.RefreshToken);
        }

        public async Task RefreshTokenAsync()
        {
            var authConfiguration = await GetAuthConfigurationAsync();
            var refreshToken = authConfiguration.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new Exception("Refresh token not found.");
            }

               var initializer = new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = authConfiguration.Credentials.client_id,
                    ClientSecret = authConfiguration.Credentials.client_secret
                },
            };

            var flow = new GoogleAuthorizationCodeFlow(initializer);
            var token = await flow.RefreshTokenAsync("user", refreshToken, CancellationToken.None);
            logger.LogInformation("New Token: {0}", JsonSerializer.Serialize(token));
            accessToken = token.AccessToken;
            AuthStateChanged?.Invoke();
        }

        public string GetAuthorizeUrl(string? redirectUri = null)
        {
            var authConfiguration = GetLegacyAuthConfiguration();
            return BuildAuthorizeUrl(authConfiguration.Credentials, redirectUri);
        }

        public async Task<string> GetAuthorizeUrlAsync(string? redirectUri = null)
        {
            var authConfiguration = await GetAuthConfigurationAsync();
            return BuildAuthorizeUrl(authConfiguration.Credentials, redirectUri);
        }

        public async Task<Google.Apis.Auth.OAuth2.Responses.TokenResponse> ExchangeCodeAsync(string code, string? redirectUri = null)
        {
            var authConfiguration = await GetAuthConfigurationAsync();
            var effectiveRedirectUri = !string.IsNullOrWhiteSpace(redirectUri) ? redirectUri : authConfiguration.Credentials.RedirectUri;
            var initializer = new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = authConfiguration.Credentials.client_id,
                    ClientSecret = authConfiguration.Credentials.client_secret
                },
            };

            var flow = new GoogleAuthorizationCodeFlow(initializer);

            var token = await flow.ExchangeCodeForTokenAsync("user", code, effectiveRedirectUri, CancellationToken.None);
            logger.LogInformation("Token: {0}", JsonSerializer.Serialize(token));
            accessToken = token.AccessToken;

            if (systemSettingsService != null && !string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                await systemSettingsService.UpdateYouTubeSettingsAsync(settings =>
                {
                    settings.RefreshToken = token.RefreshToken;
                });
            }

            AuthStateChanged?.Invoke();
            return token;
        }

        /// <summary>
        /// 登出：清除 access token
        /// </summary>
        public void Logout()
        {
            accessToken = "";
            logger.LogInformation("YouTube OAuth logged out.");
            AuthStateChanged?.Invoke();
        }

        private (YoutubeCredentials Credentials, string RefreshToken) GetLegacyAuthConfiguration()
        {
            return CreateAuthConfiguration(GetBundledYouTubeSettings());
        }

        private async Task<(YoutubeCredentials Credentials, string RefreshToken)> GetAuthConfigurationAsync()
        {
            var settings = await GetEffectiveYouTubeSettingsAsync();
            return CreateAuthConfiguration(settings);
        }

        private async Task<YouTubeApiSettings> GetEffectiveYouTubeSettingsAsync()
        {
            var bundledSettings = GetBundledYouTubeSettings();
            if (systemSettingsService == null)
            {
                return bundledSettings;
            }

            var persistedSettings = await systemSettingsService.GetYouTubeSettingsAsync();
            return MergeYouTubeSettings(bundledSettings, persistedSettings);
        }

        private YouTubeApiSettings GetBundledYouTubeSettings()
        {
            return new YouTubeApiSettings
            {
                ApiKey = configuration["YouTubeApi:ApiKey"] ?? "",
                CredentialsBase64 = configuration["YouTubeApi:CredentialsBase64"] ?? "",
                RedirectUri = configuration["YouTubeApi:RedirectUri"] ?? "",
                RefreshToken = configuration["YouTubeApi:RefreshToken"] ?? ""
            };
        }

        private static YouTubeApiSettings MergeYouTubeSettings(YouTubeApiSettings bundledSettings, YouTubeApiSettings? persistedSettings)
        {
            persistedSettings ??= new YouTubeApiSettings();

            return new YouTubeApiSettings
            {
                ApiKey = !string.IsNullOrWhiteSpace(bundledSettings.ApiKey)
                    ? bundledSettings.ApiKey
                    : persistedSettings.ApiKey ?? "",
                CredentialsBase64 = !string.IsNullOrWhiteSpace(bundledSettings.CredentialsBase64)
                    ? bundledSettings.CredentialsBase64
                    : persistedSettings.CredentialsBase64 ?? "",
                RedirectUri = !string.IsNullOrWhiteSpace(bundledSettings.RedirectUri)
                    ? bundledSettings.RedirectUri
                    : persistedSettings.RedirectUri ?? "",
                RefreshToken = !string.IsNullOrWhiteSpace(persistedSettings.RefreshToken)
                    ? persistedSettings.RefreshToken
                    : bundledSettings.RefreshToken ?? ""
            };
        }

        private static (YoutubeCredentials Credentials, string RefreshToken) CreateAuthConfiguration(YouTubeApiSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.CredentialsBase64))
            {
                throw new Exception("YouTube API credentials not found.");
            }

            var jsonBytes = Convert.FromBase64String(settings.CredentialsBase64);
            using var stream = new MemoryStream(jsonBytes);
            var data = GoogleClientSecrets.FromStream(stream).Secrets;

            return (
                new YoutubeCredentials(data!.ClientId, data!.ClientSecret, settings.RedirectUri ?? ""),
                settings.RefreshToken ?? "");
        }

        private static string BuildAuthorizeUrl(YoutubeCredentials credentials, string? redirectUri = null)
        {
            var effectiveRedirectUri = !string.IsNullOrWhiteSpace(redirectUri) ? redirectUri : credentials.RedirectUri;
            return $"{authorizationEndpoint}?response_type=code&client_id={credentials.client_id}&redirect_uri={Uri.EscapeDataString(effectiveRedirectUri)}&scope={Uri.EscapeDataString(string.Join(" ", Scopes))}&access_type=offline&include_granted_scopes=true&prompt=consent";
        }


        public async Task<YoutubeInfo> GetChannelInfoAsync()
        {
            try
            {
                // 创建 HttpClient 实例
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // 请求 YouTube 数据
                var response = await httpClient.GetAsync("https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true");

                if (response.IsSuccessStatusCode)
                {
                    var chs = await response.Content.ReadFromJsonAsync<Google.Apis.YouTube.v3.Data.ChannelListResponse>();
                    if (chs?.Items == null || chs.Items.Count == 0 || chs.Items[0].Snippet == null)
                    {
                        logger.LogInformation("YouTube channel info response was empty.");
                        return new YoutubeInfo();
                    }

                    return new YoutubeInfo
                    {
                        ChannelTitle = chs.Items[0].Snippet.Title,
                        ChannelDescription = chs.Items[0].Snippet.Description
                    };

                }
                else
                {
                    logger.LogInformation($"Error: {response.StatusCode}");
                    return new YoutubeInfo();
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Error retrieving channel info: " + ex.Message);
                return new YoutubeInfo();
            }
        }


        public async Task<string?> GetLiveChatIDFromLiveUrlAsync(string youtubeUrl)
        {
            string liveID = GetYouTubeLiveID(youtubeUrl);
            
            if (string.IsNullOrWhiteSpace(liveID))
            {
                logger.LogInformation("無法解析 YouTube URL。直播地址尚未開始直播或已結束。");
                throw new Exception("直播地址尚未開始直播或已結束");
            }

            var liveInfo =await GetYoutubeLiveInfoAsync(liveID);
            if (liveInfo == null)
            {
                logger.LogInformation("直播地址尚未開始直播或已結束");
                throw new Exception("直播地址尚未開始直播或已結束");
            }

            return liveInfo.ActiveLiveChatId;
        }


        public async Task<IEnumerable<Google.Apis.YouTube.v3.Data.LiveChatMessage>> ListLiveChatMessageAsync(string chatID)
        {
            var apiKey = await GetConfiguredApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("YouTube API key is required. Please configure the API key first.");
            }

            using var httpClient = new HttpClient();

            var response = await httpClient.GetAsync($"https://www.googleapis.com/youtube/v3/liveChat/messages?liveChatId={Uri.EscapeDataString(chatID)}&part=snippet,authorDetails&key={Uri.EscapeDataString(apiKey)}");

            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<Google.Apis.YouTube.v3.Data.LiveChatMessageListResponse>();
                if (info == null)
                {
                    return [];
                }

                return info.Items;
            }

            logger.LogInformation($"Error: {response.StatusCode}");
            return [];
        }


        public async Task<Google.Apis.YouTube.v3.Data.VideoLiveStreamingDetails?> GetYoutubeLiveInfoAsync(string liveID)
        {
            var apiKey = await GetConfiguredApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("YouTube API key is required. Please configure the API key first.");
            }

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id={Uri.EscapeDataString(liveID)}&key={Uri.EscapeDataString(apiKey)}");

            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<Google.Apis.YouTube.v3.Data.VideoListResponse>();
                return info?.Items.FirstOrDefault(o => o.LiveStreamingDetails != null)?.LiveStreamingDetails;
            }

            logger.LogInformation($"Error: {response.StatusCode}");
            return null;
        }

        private async Task<string> GetConfiguredApiKeyAsync()
        {
            var settings = await GetEffectiveYouTubeSettingsAsync();
            return settings.ApiKey ?? string.Empty;
        }

        public static string GetYouTubeLiveID(string url)
        {
            // 正則表達式來匹配 YouTube Video ID
            var regex = new Regex(@"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:watch\?v=|live\/|embed\/|v\/|.+\?v=)|youtu\.be\/)([\w_-]+)");
            var match = regex.Match(url);

            if (match.Success)
            {
                return match.Groups[1].Value;  // 取得 Video ID
            }

            return "";  // 若無法匹配則返回 null
        }

        public ChannelReader<LiveChatMessageInfo> ListenLiveChatMessagesByGrpcAsync(string liveChatId, CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<LiveChatMessageInfo>();

            _ = Task.Run(async () =>
            {
                var grpcUrl = configuration["Grpc:LiveChatServiceUrl"] ?? "https://youtube.googleapis.com";
                var accessToken = this.accessToken;

                using var grpcChannel = GrpcChannel.ForAddress(grpcUrl);
                var client = new V3DataLiveChatMessageService.V3DataLiveChatMessageServiceClient(grpcChannel);

                string? nextPageToken = null;
                var metadata = new Metadata();
                if (!string.IsNullOrWhiteSpace(accessToken))
                    metadata.Add("authorization", $"Bearer {accessToken}");

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var request = new LiveChatMessageListRequest
                        {
                            LiveChatId = liveChatId,
                            MaxResults = 20,
                        };
                        if (!string.IsNullOrEmpty(nextPageToken))
                            request.PageToken = nextPageToken;
                        request.Part.Add("snippet");
                        request.Part.Add("authorDetails");

                        using var call = client.StreamList(request, metadata, cancellationToken: cancellationToken);

                        await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
                        {
                            foreach (var msg in response.Items)
                            {
                                var info = new LiveChatMessageInfo(
                                    msg.AuthorDetails?.ChannelId ?? "",
                                    msg.AuthorDetails?.DisplayName ?? "",
                                    msg.Snippet?.DisplayMessage ?? "",
                                    msg
                                );
                                await channel.Writer.WriteAsync(info, cancellationToken);
                            }
                            nextPageToken = response.NextPageToken;
                        }

                        if (string.IsNullOrEmpty(nextPageToken))
                            break;
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    logger.LogInformation("gRPC 監聽已取消");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "gRPC 監聽發生錯誤");
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            return channel.Reader;
        }
    }
}
