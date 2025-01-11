using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EasyLotteryDomain.Models.Youtube;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EasyLotteryDomain.Services
{
    public class YouTubeServiceHelper
    {
        private readonly IConfiguration configuration;

        private static readonly string[] Scopes = { YouTubeService.Scope.YoutubeReadonly, "https://www.googleapis.com/auth/youtube.channel-memberships.creator" };

        private const string authorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";


        private readonly string apiKey;

        private string accessToken="";

        private readonly string refreshToken;

        internal record YoutubeCredentials(string client_id, string client_secret, string RedirectUri);

        private YoutubeCredentials credentials;

        private readonly ILogger<YouTubeServiceHelper> logger;

        public YouTubeServiceHelper(IConfiguration configuration, ILogger<YouTubeServiceHelper> logger)
        {
            this.logger = logger;
            this.configuration = configuration;
            apiKey =  configuration["YouTubeApi:ApiKey"]!;
            refreshToken = configuration["YouTubeApi:RefreshToken"]!;
            LoadCredentials();
        }

        private void LoadCredentials()
        {
            var base64Credential = configuration["YouTubeApi:CredentialsBase64"];
            if (base64Credential == null)
            {
                throw new Exception("YouTube API credentials not found.");
            }

            var jsonBytes = Convert.FromBase64String(base64Credential!);
            if (jsonBytes == null)
            {
                throw new Exception("Invalid YouTube API credentials.");
            }

            using var stream = new MemoryStream(jsonBytes);
            var data = GoogleClientSecrets.FromStream(stream).Secrets;

            credentials = new YoutubeCredentials(data!.ClientId, data!.ClientSecret, configuration["YouTubeApi:RedirectUri"]!);
        }

        public async Task RefreshTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new Exception("Refresh token not found.");
            }

            if (credentials == null)
            {
                throw new Exception("Credentials not found.");
            }

               var initializer = new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = credentials.client_id,
                    ClientSecret = credentials.client_secret
                },
            };

            var flow = new GoogleAuthorizationCodeFlow(initializer);
            var token = await flow.RefreshTokenAsync("user", refreshToken, CancellationToken.None);
            logger.LogInformation("New Token: {0}", JsonSerializer.Serialize(token));
            accessToken = token.AccessToken;
        }

        public  string GetAuthorizeUrl()
        {
            var authorizationUrl = $"{authorizationEndpoint}?response_type=code&client_id={credentials.client_id}&redirect_uri={credentials.RedirectUri}&scope={string.Join(" ",Scopes)}&access_type=offline&include_granted_scopes=true&prompt=consent";
           return authorizationUrl;
        }

        public async Task<Google.Apis.Auth.OAuth2.Responses.TokenResponse> ExchangeCodeAsync(string code)
        {
            var initializer = new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = credentials.client_id,
                    ClientSecret = credentials.client_secret
                },
            };

            var flow = new GoogleAuthorizationCodeFlow(initializer);

            var token = await flow.ExchangeCodeForTokenAsync("user", code, credentials.RedirectUri, CancellationToken.None);
            logger.LogInformation("Token: {0}", JsonSerializer.Serialize(token));
            return token;
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
              using var httpClient = new HttpClient();
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                }
               
                var response = await httpClient.GetAsync($"https://www.googleapis.com/youtube/v3/liveChat/messages?liveChatId={chatID}&part=snippet,authorDetails&key={apiKey}");

                if (response.IsSuccessStatusCode)
                {
                    var info = await response.Content.ReadFromJsonAsync<Google.Apis.YouTube.v3.Data.LiveChatMessageListResponse>();
                    if (info == null)
                    {
                         return [];
                    }
                    return info.Items;
                }
                else
                {
                    logger.LogInformation($"Error: {response.StatusCode}");
                    return [];
                }
        }


        public async Task<Google.Apis.YouTube.v3.Data.VideoLiveStreamingDetails?> GetYoutubeLiveInfoAsync(string liveID)
        {
                using var httpClient = new HttpClient();
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                }
                var response = await httpClient.GetAsync($"https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id={liveID}&key={apiKey}");

                if (response.IsSuccessStatusCode)
                {
                    var info = await response.Content.ReadFromJsonAsync<Google.Apis.YouTube.v3.Data.VideoListResponse>();
                    return info?.Items.FirstOrDefault(o=>o.LiveStreamingDetails != null)?.LiveStreamingDetails;
                    
                }
                else
                {
                    logger.LogInformation($"Error: {response.StatusCode}");
                    return null;
                }
        }

        public static string GetYouTubeLiveID(string url)
        {
            // 正則表達式來匹配 YouTube Video ID
           var regex = new Regex(@"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:watch\?v=|live\/|embed\/|v\/|.+\?v=)|youtu\.be\/)([a-zA-Z0-9]{11})");
            var match = regex.Match(url);

            if (match.Success)
            {
                return match.Groups[1].Value;  // 取得 Video ID
            }

            return "";  // 若無法匹配則返回 null
        }
    }
}