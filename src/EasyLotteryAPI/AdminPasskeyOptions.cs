using Fido2NetLib;

namespace EasyLotteryApi;

public sealed class AdminPasskeyOptions
{
    public const string DefaultEmail = "admin@example.com";
    public const string RegisterFlow = "register";
    public const string LoginFlow = "login";
    public const string AddFlow = "add";

    public required string Email { get; init; }
    public required string RpId { get; init; }
    public required string ServerName { get; init; }
    public required HashSet<string> Origins { get; init; }
    public required TimeSpan ChallengeLifetime { get; init; }

    public Fido2Configuration CreateFido2Configuration() => new()
    {
        ServerDomain = RpId,
        ServerName = ServerName,
        Origins = Origins
    };

    public static AdminPasskeyOptions Load(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Admin:Passkey:Origins")
            .GetChildren()
            .Select(item => item.Value?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var singleOrigin = configuration["Admin:Passkey:Origin"]?.Trim();
        if (!string.IsNullOrWhiteSpace(singleOrigin)) origins.Add(singleOrigin);
        if (origins.Count == 0)
        {
            origins.UnionWith(["http://localhost:18930", "https://localhost:18930", "http://127.0.0.1:18930", "https://127.0.0.1:18930"]);
        }

        var challengeMinutes = configuration.GetValue("Admin:Passkey:ChallengeMinutes", 2);
        return new AdminPasskeyOptions
        {
            Email = NormalizeEmail(configuration["Admin:Email"] ?? configuration["admin.email"] ?? DefaultEmail),
            RpId = (configuration["Admin:Passkey:RpId"] ?? "localhost").Trim().ToLowerInvariant(),
            ServerName = (configuration["Admin:Passkey:ServerName"] ?? "EasyLottery").Trim(),
            Origins = origins,
            ChallengeLifetime = TimeSpan.FromMinutes(Math.Clamp(challengeMinutes, 1, 10))
        };
    }

    public bool IsAdminEmail(string? email) =>
        string.Equals(NormalizeEmail(email), Email, StringComparison.Ordinal);

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();
}
