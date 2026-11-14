using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EasyLotteryCardHub.Security;

public static class HubAuthenticationExtensions
{
    public static IServiceCollection AddHubAuthentication(this IServiceCollection services, HubTokenSecurityOptions options, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = HubAuthSchemes.Jwt;
                options.DefaultChallengeScheme = HubAuthSchemes.Jwt;
            })
            .AddJwtBearer(HubAuthSchemes.Jwt, jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudiences = HubTokenAudiences.All,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(15),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = options.SigningKey
                };
            })
            .AddScheme<AuthenticationSchemeOptions, PlatformAdminKeyAuthenticationHandler>(HubAuthSchemes.PlatformAdminKey, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(HubPolicies.PlatformAdmin, policy => policy
                .AddAuthenticationSchemes(HubAuthSchemes.PlatformAdminKey)
                .RequireAuthenticatedUser())
            .AddPolicy(HubPolicies.Streamer, policy => policy
                .AddAuthenticationSchemes(HubAuthSchemes.Jwt)
                .RequireAuthenticatedUser()
                .RequireClaim("aud", HubTokenAudiences.Streamer)
                .RequireClaim("token_kind", "streamer"))
            .AddPolicy(HubPolicies.Site, policy => policy
                .AddAuthenticationSchemes(HubAuthSchemes.Jwt)
                .RequireAuthenticatedUser()
                .RequireClaim("aud", HubTokenAudiences.Site)
                .RequireClaim("token_kind", "site"));
        return services;
    }
}

public sealed class PlatformAdminKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var supplied = Request.Headers["X-Platform-Admin-Key"].ToString();
        var expected = configuration["PlatformAdmin:BootstrapToken"];
        var email = configuration["PlatformAdmin:BootstrapEmail"]?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(supplied) || string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(email))
            return Task.FromResult(AuthenticateResult.NoResult());

        var valid = SecretHasher.Verify(supplied, SecretHasher.Hash(expected));
        if (!valid) return Task.FromResult(AuthenticateResult.Fail("Invalid platform admin key."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, "platform_admin")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
