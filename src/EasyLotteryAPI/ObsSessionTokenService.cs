using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EasyLotteryApi;

public sealed class ObsSessionTokenService
{
    public const string HeaderName = "X-EasyLottery-Session-Token";
    public const string AdminUse = "admin";
    public const string ObsUse = "obs";

    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly SigningCredentials _credentials;
    private readonly TokenValidationParameters _validation;
    private readonly TimeSpan _adminTokenLifetime;
    private readonly TimeSpan _obsTokenLifetime;
    private readonly HashSet<string> _revokedTokenIds = new(StringComparer.Ordinal);
    private readonly object _revocationLock = new();

    public ObsSessionTokenService(AdminTokenSecurityOptions? options = null)
    {
        options ??= new AdminTokenSecurityOptions
        {
            Mode = AdminTokenSecurityOptions.LocalMode,
            AllowedClientIps = [],
            TrustedProxyIps = [],
            AdminTokenLifetime = TimeSpan.FromMinutes(AdminTokenSecurityOptions.DefaultAdminLifetimeMinutes),
            ObsTokenLifetime = TimeSpan.FromMinutes(AdminTokenSecurityOptions.DefaultObsLifetimeMinutes)
        };
        _adminTokenLifetime = options.AdminTokenLifetime;
        _obsTokenLifetime = options.ObsTokenLifetime;
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)) { KeyId = Guid.NewGuid().ToString("N") };
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "EasyLottery",
            ValidateAudience = true,
            ValidAudience = "EasyLottery.Api",
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = key
        };
    }

    public SessionToken IssueAdminToken(TimeSpan? lifetime = null) =>
        Issue(AdminUse, lifetime ?? _adminTokenLifetime, []);

    public SessionToken IssueObsToken(string resourceKind, string resourceId, IEnumerable<string> scopes, TimeSpan? lifetime = null)
    {
        var normalizedScopes = scopes.Select(Normalize).Where(scope => scope.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        if (normalizedScopes.Length == 0) throw new ArgumentException("OBS token 至少需要一個 scope。", nameof(scopes));
        return Issue(ObsUse, lifetime ?? _obsTokenLifetime,
        [
            new Claim("resource_kind", Normalize(resourceKind)),
            new Claim("resource_id", NormalizeResourceId(resourceId)),
            .. normalizedScopes.Select(scope => new Claim("scope", scope))
        ]);
    }

    public ClaimsPrincipal? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var principal = _handler.ValidateToken(token, _validation, out _);
            var tokenId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            lock (_revocationLock)
            {
                if (tokenId is not null && _revokedTokenIds.Contains(tokenId)) return null;
            }
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public bool IsAdmin(string? token) => Validate(token)?.FindFirst("token_use")?.Value == AdminUse;

    public bool CanAccessObs(string? token, string resourceKind, string resourceId, string requiredScope)
    {
        var principal = Validate(token);
        if (principal is null) return false;
        if (principal.FindFirst("token_use")?.Value == AdminUse) return true;
        return principal.FindFirst("token_use")?.Value == ObsUse
            && principal.FindFirst("resource_kind")?.Value == Normalize(resourceKind)
            && principal.FindFirst("resource_id")?.Value == NormalizeResourceId(resourceId)
            && principal.FindAll("scope").Any(claim => claim.Value == Normalize(requiredScope));
    }

    public void Revoke(string token)
    {
        var tokenId = Validate(token)?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (tokenId is null) return;
        lock (_revocationLock) _revokedTokenIds.Add(tokenId);
    }

    private SessionToken Issue(string tokenUse, TimeSpan lifetime, IEnumerable<Claim> additionalClaims)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(lifetime);
        var jwt = new JwtSecurityToken(
            issuer: "EasyLottery",
            audience: "EasyLottery.Api",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, $"easy-lottery-{tokenUse}"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim("token_use", tokenUse),
                .. additionalClaims
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);
        return new SessionToken(_handler.WriteToken(jwt), expires);
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private static string NormalizeResourceId(string value) => Guid.TryParse(value, out var guid) ? guid.ToString("N") : Normalize(value);
}

public sealed record SessionToken(string Token, DateTimeOffset ExpiresAtUtc);
