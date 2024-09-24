using System.Net.Http.Json;
using System.Text.RegularExpressions;
using EasyLotteryDomain.Models.Youtube;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryDomain.Services
{
    public class YouTubeServiceHelper
    {
        private readonly IConfiguration _configuration;

        private static readonly string[] Scopes = { YouTubeService.Scope.YoutubeReadonly, "https://www.googleapis.com/auth/youtube.channel-memberships.creator" };
        private readonly string applicationName;
        private readonly string credentialPath;

        private readonly string apiKey;

        public YouTubeServiceHelper()
        {
          
        }
        public YouTubeServiceHelper(IConfiguration configuration, string applicationName, string credentialPath)
        {
            this._configuration = configuration;
            this.applicationName = applicationName;
            this.credentialPath = credentialPath;
        }

        public YouTubeServiceHelper(IConfiguration configuration)
        {
            this._configuration = configuration;
            this.apiKey =  configuration["YouTubeApi:ApiKey"]!;
        }

        private async Task<UserCredential> GetUserCredentialAsync()
        {
            var base64Credential = _configuration["YouTubeApi:CredentialsBase64"];
            var jsonBytes = Convert.FromBase64String(base64Credential!);
            using var stream = new MemoryStream(jsonBytes);
            return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credentialPath, true));
        }

        public async Task<YouTubeService> GetYouTubeServiceAsync()
        {
            var credential = await GetUserCredentialAsync();

            return new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = this.applicationName,
            });
        }



        public async Task<IList<Google.Apis.YouTube.v3.Data.Member>> ListChannelMembersAsync()
        {
            var youtubeService = await GetYouTubeServiceAsync();

            var request = youtubeService.Members.List("snippet");

            var response = await request.ExecuteAsync();

            foreach (var member in response.Items)
            {
                Console.WriteLine($"Member: {member.Snippet.MemberDetails.DisplayName}");
            }

            return response.Items;
        }

        public async Task<YoutubeInfo> GetChannelInfoAsync(string accessToken)
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
                    Console.WriteLine($"Error: {response.StatusCode}");
                    return new YoutubeInfo();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving channel info: " + ex.Message);
                return new YoutubeInfo();
            }
        }


        public async Task<string?> GetLiveChatIDFromLiveUrlAsync(string youtubeUrl)
        {
            string liveID = GetYouTubeLiveID(youtubeUrl);
            
            if (liveID == null)
            {
                Console.WriteLine("無法解析 YouTube URL。");
                throw new Exception("直播地址尚未開始直播或已結束");
            }

            var liveInfo =await GetYoutubeLiveInfoAsync(liveID);
            if (liveInfo == null)
            {
                Console.WriteLine("直播地址尚未開始直播或已結束");
                throw new Exception("直播地址尚未開始直播或已結束");
            }

            return liveInfo.ActiveLiveChatId;
        }


        public async Task<IEnumerable<Google.Apis.YouTube.v3.Data.LiveChatMessage>> ListLiveChatMessageAsync(string chatID)
        {
              using var httpClient = new HttpClient();
                // 请求 YouTube 数据
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
                    Console.WriteLine($"Error: {response.StatusCode}");
                    return [];
                }
        }


        public async Task<Google.Apis.YouTube.v3.Data.VideoLiveStreamingDetails?> GetYoutubeLiveInfoAsync(string liveID)
        {
                using var httpClient = new HttpClient();
                // 请求 YouTube 数据
                var response = await httpClient.GetAsync($"https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id={liveID}&key={apiKey}");

                if (response.IsSuccessStatusCode)
                {
                    var info = await response.Content.ReadFromJsonAsync<Google.Apis.YouTube.v3.Data.VideoListResponse>();
                    return info?.Items.FirstOrDefault(o=>o.LiveStreamingDetails != null)?.LiveStreamingDetails;
                    
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    return null;
                }
        }

        public static string GetYouTubeLiveID(string url)
        {
            // 正則表達式來匹配 YouTube Video ID
            var regex = new Regex(@"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:watch\?v=|embed\/|v\/|.+\?v=)|youtu\.be\/)([a-zA-Z0-9_-]{11})");
            var match = regex.Match(url);

            if (match.Success)
            {
                return match.Groups[1].Value;  // 取得 Video ID
            }

            return null;  // 若無法匹配則返回 null
        }
    }
}