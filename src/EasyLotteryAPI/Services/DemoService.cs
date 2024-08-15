using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;

namespace EasyLotteryAPI.Services
{
    public class DemoService
    {
        private static readonly string[] Scopes = { YouTubeService.Scope.YoutubeReadonly, "https://www.googleapis.com/auth/youtube.channel-memberships.creator"};


        public static async Task<List<Google.Apis.YouTube.v3.Data.Member>> GetChannelMembers()
        {
            try
            {
                var jsonBytes = Convert.FromBase64String("eyJ3ZWIiOnsiY2xpZW50X2lkIjoiMzkyNDcxMjgxNDY5LTNwdWhxNDVhbDlqZDFjMDE1a3M1MzVicTRxbWw2aDg5LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwicHJvamVjdF9pZCI6ImRlcmVrcHJvamVjdC0zZTZmOSIsImF1dGhfdXJpIjoiaHR0cHM6Ly9hY2NvdW50cy5nb29nbGUuY29tL28vb2F1dGgyL2F1dGgiLCJ0b2tlbl91cmkiOiJodHRwczovL29hdXRoMi5nb29nbGVhcGlzLmNvbS90b2tlbiIsImF1dGhfcHJvdmlkZXJfeDUwOV9jZXJ0X3VybCI6Imh0dHBzOi8vd3d3Lmdvb2dsZWFwaXMuY29tL29hdXRoMi92MS9jZXJ0cyIsImNsaWVudF9zZWNyZXQiOiJHT0NTUFgtMnJfblAwNi1hMkJPVlU0WmxXRnJSWGRrXzNOaSJ9fQ==");
                using var stream = new MemoryStream(jsonBytes);
                var credential =  await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore("certs", true));

                var svc =  new YouTubeService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "EasyLottery",
                });

                var request = svc.Members.List("snippet");

                var response = await request.ExecuteAsync();

                foreach (var member in response.Items)
                {
                    Console.WriteLine($"Member: {member.Snippet.MemberDetails.DisplayName}");
                }

               return response.Items.ToList();

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving channel members: " + ex.Message);
                return new List<Google.Apis.YouTube.v3.Data.Member>();
            }
        }
    }
}