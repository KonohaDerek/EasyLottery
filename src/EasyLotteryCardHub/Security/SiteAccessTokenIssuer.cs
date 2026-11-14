using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace EasyLotteryCardHub.Security;

public sealed class SiteAccessTokenIssuer(IConfiguration configuration, TimeProvider timeProvider)
{
    public const string Audience = "card-hub-site";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public (string Token, int ExpiresIn) Issue(Guid tenantId, Guid siteId, string clientId)
        => Issue(tenantId, new Dictionary<string, string>
        {
            ["site_id"] = siteId.ToString("D"),
            ["client_id"] = clientId
        }, "site");

    public (string Token, int ExpiresIn) IssueStreamer(Guid tenantId, Guid accountId)
        => Issue(tenantId, new Dictionary<string, string>
        {
            ["streamer_account_id"] = accountId.ToString("D")
        }, "streamer");

    public (string Token, int ExpiresIn) IssueViewer(Guid viewerId, string ageBracket)
        => Issue(viewerId, new Dictionary<string, string>
        {
            ["viewer_id"] = viewerId.ToString("D"),
            ["age_bracket"] = ageBracket
        }, "viewer");

    public bool TryValidateStreamerToken(string token, out Guid tenantId, out Guid accountId)
    {
        tenantId = Guid.Empty;
        accountId = Guid.Empty;
        try
        {
            var key = ReadSigningKey();
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["CardHub:SiteToken:Issuer"] ?? "EasyLotteryCardHub",
                ValidateAudience = true,
                ValidAudience = "card-hub-streamer",
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            return principal.FindFirst("role")?.Value == "streamer"
                && Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId)
                && Guid.TryParse(principal.FindFirst("streamer_account_id")?.Value, out accountId);
        }
        catch (Exception exception) when (exception is SecurityTokenException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    public bool TryValidateSiteToken(string token, out Guid tenantId, out Guid siteId, out string clientId)
    {
        tenantId = Guid.Empty;
        siteId = Guid.Empty;
        clientId = string.Empty;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["CardHub:SiteToken:Issuer"] ?? "EasyLotteryCardHub",
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(ReadSigningKey()),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
            clientId = principal.FindFirst("client_id")?.Value ?? string.Empty;
            return principal.FindFirst("role")?.Value == "site"
                && Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId)
                && Guid.TryParse(principal.FindFirst("site_id")?.Value, out siteId)
                && clientId.Length > 0;
        }
        catch (Exception exception) when (exception is SecurityTokenException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    public bool TryValidateViewerToken(string token, out Guid viewerId, out string ageBracket)
    {
        viewerId = Guid.Empty;
        ageBracket = string.Empty;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["CardHub:SiteToken:Issuer"] ?? "EasyLotteryCardHub",
                ValidateAudience = true,
                ValidAudience = "card-hub-viewer",
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(ReadSigningKey()),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
            ageBracket = principal.FindFirst("age_bracket")?.Value ?? string.Empty;
            return principal.FindFirst("role")?.Value == "viewer"
                && Guid.TryParse(principal.FindFirst("viewer_id")?.Value, out viewerId)
                && ageBracket is "13_17" or "18_plus";
        }
        catch (Exception exception) when (exception is SecurityTokenException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private (string Token, int ExpiresIn) Issue(Guid tenantId, IReadOnlyDictionary<string, string> extraClaims, string role)
    {
        var key = ReadSigningKey();
        var now = timeProvider.GetUtcNow();
        var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, extraClaims.GetValueOrDefault("client_id") ?? extraClaims.GetValueOrDefault("streamer_account_id") ?? extraClaims.GetValueOrDefault("viewer_id")!),
            new("tenant_id", tenantId.ToString("D")),
            new("role", role)
        };
        foreach (var (name, value) in extraClaims.Where(item => item.Key != "client_id")) claims.Add(new Claim(name, value));
        if (role == "site" && extraClaims.TryGetValue("client_id", out var clientId)) claims.Add(new Claim("client_id", clientId));
        var jwt = new JwtSecurityToken(
            issuer: configuration["CardHub:SiteToken:Issuer"] ?? "EasyLotteryCardHub",
            audience: role switch { "site" => Audience, "streamer" => "card-hub-streamer", "viewer" => "card-hub-viewer", _ => throw new ArgumentOutOfRangeException(nameof(role)) },
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.Add(Lifetime).UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), (int)Lifetime.TotalSeconds);
    }

    private byte[] ReadSigningKey()
    {
        var rawKey = configuration["CardHub:SiteToken:SigningKey"];
        if (string.IsNullOrWhiteSpace(rawKey))
            throw new InvalidOperationException("CardHub:SiteToken:SigningKey must be configured before issuing tokens.");
        byte[] key;
        try { key = Convert.FromBase64String(rawKey); }
        catch (FormatException exception) { throw new InvalidOperationException("CardHub:SiteToken:SigningKey must be base64-encoded.", exception); }
        if (key.Length < 32)
            throw new InvalidOperationException("CardHub:SiteToken:SigningKey must contain at least 32 bytes.");
        return key;
    }
}
