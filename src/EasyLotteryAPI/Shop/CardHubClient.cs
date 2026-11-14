using System.Net;
using System.Text.Json;

namespace EasyLotteryApi.Shop;

/// <summary>Minimal typed client for verifying site credentials with Card Hub.</summary>
public sealed class CardHubClient(HttpClient httpClient) : IDisposable
{
    public const string TokenEndpointPath = "/connect/token";

    public void Dispose() => httpClient.Dispose();

    public async Task<string> TestConnectionAsync(
        string hubBaseUrl,
        string siteClientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{hubBaseUrl.TrimEnd('/')}{TokenEndpointPath}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = siteClientId,
                ["client_secret"] = clientSecret
            })
        };
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HasAccessToken(body) ? HubLinkStatuses.Linked : HubLinkStatuses.HubUnreachable;
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                && IsInvalidClient(body))
            {
                return HubLinkStatuses.CredentialsInvalid;
            }

            if ((int)response.StatusCode >= 500) return HubLinkStatuses.HubUnreachable;
            return HubLinkStatuses.HubUnreachable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException or JsonException)
        {
            return HubLinkStatuses.HubUnreachable;
        }
    }

    private static bool HasAccessToken(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("access_token", out var token)
            && token.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(token.GetString());
    }

    private static bool IsInvalidClient(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String
            && string.Equals(error.GetString(), "invalid_client", StringComparison.Ordinal);
    }
}
