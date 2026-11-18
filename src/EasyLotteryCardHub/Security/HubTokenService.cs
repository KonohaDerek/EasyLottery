using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EasyLotteryCardHub.Security;

public static class HubTokenAudiences
{
    public const string Streamer = "card-hub-streamer";
    public const string Site = "card-hub-site";
    public const string Viewer = "card-hub-viewer";

    // The viewer audience is reserved now so 1b can add its token issuer without
    // widening the site or streamer authorization policies.
    public static IReadOnlyCollection<string> All { get; } = [Streamer, Site, Viewer];
}

public static class HubAuthSchemes
{
    public const string Jwt = "HubJwt";
    public const string PlatformAdminKey = "PlatformAdminKey";
}

public static class HubPolicies
{
    public const string PlatformAdmin = "platform-admin";
    public const string Streamer = "streamer";
    public const string Site = "site";
    public const string Viewer = "viewer";
}

public sealed class HubTokenSecurityOptions
{
    public required string Issuer { get; init; }
    public required SymmetricSecurityKey SigningKey { get; init; }
    public required TimeSpan StreamerLifetime { get; init; }
    public required TimeSpan SiteLifetime { get; init; }
    public required TimeSpan ViewerLifetime { get; init; }

    public static HubTokenSecurityOptions Load(IConfiguration configuration)
    {
        var signingSecret = configuration["Authentication:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingSecret) || Encoding.UTF8.GetByteCount(signingSecret) < 32)
            throw new InvalidOperationException("Authentication:SigningKey must contain at least 32 UTF-8 bytes.");

        return new HubTokenSecurityOptions
        {
            Issuer = configuration["Authentication:Issuer"]?.Trim() is { Length: > 0 } issuer
                ? issuer
                : "https://card-hub.easylottery.invalid",
            SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret)),
            StreamerLifetime = TimeSpan.FromMinutes(15),
            SiteLifetime = TimeSpan.FromMinutes(10),
            ViewerLifetime = TimeSpan.FromMinutes(15)
        };
    }
}

public sealed record IssuedHubToken(string AccessToken, DateTimeOffset ExpiresAt);

public sealed class HubTokenService(HubTokenSecurityOptions options, TimeProvider timeProvider)
{
    public IssuedHubToken IssueStreamerToken(Guid tenantId)
    {
        var expiresAt = timeProvider.GetUtcNow().Add(options.StreamerLifetime);
        return Issue(HubTokenAudiences.Streamer, expiresAt,
        [
            new Claim(JwtRegisteredClaimNames.Sub, tenantId.ToString("D")),
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim("token_kind", "streamer")
        ]);
    }

    public IssuedHubToken IssueSiteToken(Guid tenantId, Guid siteId, Guid credentialId)
    {
        var expiresAt = timeProvider.GetUtcNow().Add(options.SiteLifetime);
        return Issue(HubTokenAudiences.Site, expiresAt,
        [
            new Claim(JwtRegisteredClaimNames.Sub, siteId.ToString("D")),
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim("site_id", siteId.ToString("D")),
            new Claim("site_credential_id", credentialId.ToString("D")),
            new Claim("token_kind", "site")
        ]);
    }

    public IssuedHubToken IssueViewerToken(Guid viewerId)
    {
        var expiresAt = timeProvider.GetUtcNow().Add(options.ViewerLifetime);
        return Issue(HubTokenAudiences.Viewer, expiresAt,
        [
            new Claim(JwtRegisteredClaimNames.Sub, viewerId.ToString("D")),
            new Claim("viewer_id", viewerId.ToString("D")),
            new Claim("token_kind", "viewer")
        ]);
    }

    private IssuedHubToken Issue(string audience, DateTimeOffset expiresAt, IEnumerable<Claim> claims)
    {
        var now = timeProvider.GetUtcNow();
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: audience,
            claims: claims.Append(new Claim(JwtRegisteredClaimNames.Jti, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant())),
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(options.SigningKey, SecurityAlgorithms.HmacSha256));
        return new IssuedHubToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
