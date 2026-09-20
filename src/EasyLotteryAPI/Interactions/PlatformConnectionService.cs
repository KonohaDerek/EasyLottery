using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Interactions;
using EasyLotteryInfrastructure.Settings;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Net.Http.Json;

namespace EasyLotteryApi.Interactions;

public interface IPlatformConnector
{
    string Platform { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed record PlatformConnectionUpdate(string? ApiKey, string? AccessToken, string? ChannelScope, bool Degrade = false);

public sealed record PlatformConnectionStatus(string Platform, string State, bool Configured, string ChannelScope, string? ApiKey, string? AccessToken);
public sealed record PlatformConnectionsSnapshot(IReadOnlyList<PlatformConnectionStatus> Connections, string ETag);
public sealed record TwitchOAuthChallenge(string State, string CodeChallenge, string AuthorizationUrl, DateTimeOffset ExpiresAtUtc);
internal sealed record PendingTwitchOAuth(string CodeVerifier, DateTimeOffset ExpiresAtUtc);

public sealed class PlatformConnectionService(IInteractionsYamlDocumentRepository repository, TimeProvider clock, IHttpClientFactory clients, IConfiguration configuration)
{
    private readonly ConcurrentDictionary<string, PendingTwitchOAuth> _twitchChallenges = new(StringComparer.Ordinal);

    public async Task<PlatformConnectionsSnapshot> ListAsync(CancellationToken cancellationToken = default)
    {
        var document = await repository.ReadAsync(cancellationToken);
        var connections = new[] { "YouTube", "Twitch", "Discord" }
            .Select(platform => StatusFor(platform, document.PlatformConnections.SingleOrDefault(item => item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))))
            .ToList();
        return new PlatformConnectionsSnapshot(connections, ETagFor(document));
    }

    public async Task<PlatformConnectionsSnapshot> UpdateAsync(string platform, PlatformConnectionUpdate update, string? expectedETag, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePlatform(platform);
        var document = await repository.ReadAsync(cancellationToken);
        var actualETag = ETagFor(document);
        if (!string.IsNullOrWhiteSpace(expectedETag) && expectedETag != "*" && !string.Equals(expectedETag.Trim(), actualETag, StringComparison.Ordinal))
            throw new ConfigurationConcurrencyException(expectedETag, actualETag);
        var settings = document.PlatformConnections.SingleOrDefault(item => item.Platform.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (settings is null)
        {
            settings = new PlatformConnectionSettings { Platform = normalized };
            document.PlatformConnections.Add(settings);
        }
        settings.ApiKey = PreserveMaskedSecret(update.ApiKey, settings.ApiKey);
        settings.AccessToken = PreserveMaskedSecret(update.AccessToken, settings.AccessToken);
        settings.ChannelScope = update.ChannelScope ?? settings.ChannelScope;
        settings.Degrade = update.Degrade;
        document.PlatformConnectionsRevision = Guid.NewGuid().ToString("N");
        await repository.SaveAsync(document, cancellationToken);
        return await ListAsync(cancellationToken);
    }

    public TwitchOAuthChallenge BeginTwitchOAuth()
    {
        var clientId = configuration["Interactions:Twitch:ClientId"];
        var redirectUri = configuration["Interactions:Twitch:RedirectUri"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            throw new InvalidOperationException("請先在伺服器設定 Twitch OAuth ClientId 與 RedirectUri。");
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        var expiresAtUtc = clock.GetUtcNow().AddMinutes(10);
        _twitchChallenges[state] = new PendingTwitchOAuth(verifier, expiresAtUtc);
        var codeChallenge = Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        var authorizationUrl = $"https://id.twitch.tv/oauth2/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString("chat:read")}&state={Uri.EscapeDataString(state)}&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256";
        return new TwitchOAuthChallenge(state, codeChallenge, authorizationUrl, expiresAtUtc);
    }

    public async Task<bool> TryCompleteTwitchOAuthAsync(string state, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code)) return false;
        if (!_twitchChallenges.TryRemove(state, out var challenge) || challenge.ExpiresAtUtc <= clock.GetUtcNow()) return false;
        var clientId = configuration["Interactions:Twitch:ClientId"];
        var clientSecret = configuration["Interactions:Twitch:ClientSecret"];
        var redirectUri = configuration["Interactions:Twitch:RedirectUri"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri)) return false;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["code_verifier"] = challenge.CodeVerifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            })
        };
        using var response = await clients.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        var token = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(token?.AccessToken)) return false;

        var document = await repository.ReadAsync(cancellationToken);
        var settings = document.PlatformConnections.SingleOrDefault(item => item.Platform.Equals("Twitch", StringComparison.OrdinalIgnoreCase));
        if (settings is null)
        {
            settings = new PlatformConnectionSettings { Platform = "Twitch" };
            document.PlatformConnections.Add(settings);
        }
        settings.AccessToken = token.AccessToken;
        settings.Degrade = false;
        document.PlatformConnectionsRevision = Guid.NewGuid().ToString("N");
        await repository.SaveAsync(document, cancellationToken);
        return true;
    }

    private static PlatformConnectionStatus StatusFor(string platform, PlatformConnectionSettings? settings)
    {
        if (settings is null)
        {
            return new PlatformConnectionStatus(platform, "unconfigured", false, "", null, null);
        }

        var configured = platform == "YouTube"
            ? !string.IsNullOrWhiteSpace(settings.ApiKey)
            : !string.IsNullOrWhiteSpace(settings.AccessToken);
        var state = !configured ? "unconfigured" : settings.Degrade ? "degraded" : "configured";
        return new PlatformConnectionStatus(platform, state, configured, settings.ChannelScope, Mask(settings.ApiKey), Mask(settings.AccessToken));
    }

    private static string NormalizePlatform(string platform) => platform.ToLowerInvariant() switch
    {
        "youtube" => "YouTube",
        "twitch" => "Twitch",
        "discord" => "Discord",
        _ => throw new InvalidOperationException("不支援的互動平台。")
    };

    private static string? Mask(string? secret) => string.IsNullOrWhiteSpace(secret) ? null : ConfigSecretRedactor.UnchangedSecretMask;

    private static string PreserveMaskedSecret(string? submitted, string existing) =>
        submitted is null || string.Equals(submitted, ConfigSecretRedactor.UnchangedSecretMask, StringComparison.Ordinal) ? existing : submitted;

    private static string ETagFor(InteractionsYamlDocument document) =>
        $"\"platform-connections-{(string.IsNullOrWhiteSpace(document.PlatformConnectionsRevision) ? "0" : document.PlatformConnectionsRevision)}\"";

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TwitchTokenResponse(string? AccessToken);
}
