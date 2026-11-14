using System.Net;
using System.Net.Http.Headers;

namespace EasyLotteryApi.CardShop;

internal enum CardHubConnectionState
{
    Linked,
    CredentialsInvalid,
    Unreachable
}

internal interface ICardHubConnectionVerifier
{
    Task<CardHubConnectionState> VerifyAsync(string hubUrl, string clientId, string clientSecret, CancellationToken cancellationToken);
}

internal sealed class CardHubConnectionVerifier(HttpClient client, CardHubOriginPolicy originPolicy) : ICardHubConnectionVerifier
{
    public async Task<CardHubConnectionState> VerifyAsync(string hubUrl, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        if (!originPolicy.TryGetAllowedOrigin(hubUrl, out var origin)) return CardHubConnectionState.Unreachable;
        var tokenUri = new Uri(origin, "/hub/v1/sites/token");

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => CardHubConnectionState.Linked,
                HttpStatusCode.Unauthorized => CardHubConnectionState.CredentialsInvalid,
                _ => CardHubConnectionState.Unreachable
            };
        }
        catch (HttpRequestException)
        {
            return CardHubConnectionState.Unreachable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CardHubConnectionState.Unreachable;
        }
    }
}
