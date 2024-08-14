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

        private static readonly string[] Scopes = { YouTubeService.Scope.YoutubeReadonly };
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
    }
}