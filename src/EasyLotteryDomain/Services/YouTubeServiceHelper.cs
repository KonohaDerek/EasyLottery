using System.Net.Http.Json;
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

        private static readonly string[] Scopes = { YouTubeService.Scope.YoutubeReadonly, "https://www.googleapis.com/auth/youtube.channel-memberships.creator"};
        private readonly string applicationName;
        private readonly string credentialPath;

        public YouTubeServiceHelper(IConfiguration configuration, string applicationName, string credentialPath)
        {
            this._configuration = configuration;
            this.applicationName = applicationName;
            this.credentialPath = credentialPath;
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
    }
}