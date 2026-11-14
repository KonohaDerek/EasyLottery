using System.Security.Cryptography;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace EasyLotteryCardHub.Invites;

public static class PasskeyConfiguration
{
    public static void ValidateProduction(IConfiguration configuration)
    {
        var rpId = configuration["CardHub:Passkey:RpId"]?.Trim();
        var origins = configuration.GetSection("CardHub:Passkey:Origins").GetChildren()
            .Select(item => item.Value?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (string.IsNullOrWhiteSpace(rpId) || string.Equals(rpId, "localhost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production requires an explicit non-localhost CardHub:Passkey:RpId.");
        if (origins.Length == 0 || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback
                || !(string.Equals(uri.Host, rpId, StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith("." + rpId, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Production requires explicit HTTPS CardHub:Passkey:Origins without localhost origins.");
    }

    public static Fido2NetLib.Fido2Configuration Create(IConfiguration configuration, bool isDevelopment)
    {
        if (!isDevelopment) ValidateProduction(configuration);
        var origins = configuration.GetSection("CardHub:Passkey:Origins").GetChildren()
            .Select(item => item.Value?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (isDevelopment && origins.Count == 0) origins.UnionWith(["http://localhost:8080", "https://localhost:8080", "http://127.0.0.1:8080"]);
        return new Fido2NetLib.Fido2Configuration
        {
            RPID = configuration["CardHub:Passkey:RpId"]?.Trim().ToLowerInvariant() ?? "localhost",
            RPName = configuration["CardHub:Passkey:ServerName"] ?? "EasyLottery Card Hub",
            Origins = origins
        };
    }

    public static AssertionOptions CreateDiscoverableAssertionOptions(IConfiguration configuration, bool isDevelopment) =>
        new Fido2(Create(configuration, isDevelopment)).GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required
        });

    public static CredentialCreateOptions CreateResidentCredentialOptions(IConfiguration configuration, bool isDevelopment, Guid inviteId, string slug, string displayName) =>
        new Fido2(Create(configuration, isDevelopment)).RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = SHA256.HashData(inviteId.ToByteArray()), Name = slug, DisplayName = displayName },
            ExcludeCredentials = [],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required
            },
            AttestationPreference = AttestationConveyancePreference.None
        });
}
