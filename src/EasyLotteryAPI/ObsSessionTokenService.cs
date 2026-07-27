using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace EasyLotteryApi;

public sealed class ObsSessionTokenService
{
    public const string HeaderName = "X-EasyLottery-Session-Token";

    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;
    public ObsSessionTokenService()
    {
        var signingKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64))
        {
            KeyId = Guid.NewGuid().ToString("N")
        };

        _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = signingKey,
        };
    }

    public (string Token, DateTimeOffset ExpiresAtUtc) IssueToken()
    {
        var issuedAtUtc = DateTimeOffset.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddHours(12);
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "easy-lottery-obs-session"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(JwtRegisteredClaimNames.Iat, issuedAtUtc.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            ],
            notBefore: issuedAtUtc.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: _signingCredentials);

        return (_tokenHandler.WriteToken(token), expiresAtUtc);
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            _tokenHandler.ValidateToken(token, _validationParameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
