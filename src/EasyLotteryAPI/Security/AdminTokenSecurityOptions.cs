using System.Net;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryApi.Security;

public sealed class AdminTokenSecurityOptions
{
    public const string LocalMode = "local";
    public const string PublicMode = "public";
    public const int DefaultAdminLifetimeMinutes = 60;
    public const int DefaultObsLifetimeMinutes = 360;

    public required string Mode { get; init; }

    public required IReadOnlyList<string> AllowedClientIps { get; init; }

    public required IReadOnlyList<IPAddress> TrustedProxyIps { get; init; }

    public required TimeSpan AdminTokenLifetime { get; init; }

    public required TimeSpan ObsTokenLifetime { get; init; }

    public string? SigningKey { get; init; }

    public static AdminTokenSecurityOptions Load(IConfiguration configuration)
    {
        var mode = (configuration["Security:AdminToken:Mode"] ?? LocalMode).Trim().ToLowerInvariant();
        var allowedClientIps = ReadValues(configuration, "Security:AdminToken:AllowedClientIps");
        var trustedProxyIps = ReadValues(configuration, "Security:AdminToken:TrustedProxyIps")
            .Select(value => IPAddress.TryParse(value, out var address) ? address : null)
            .Where(address => address is not null)
            .Cast<IPAddress>()
            .ToArray();

        return new AdminTokenSecurityOptions
        {
            Mode = mode,
            AllowedClientIps = allowedClientIps,
            TrustedProxyIps = trustedProxyIps,
            AdminTokenLifetime = TimeSpan.FromMinutes(ReadLifetimeMinutes(configuration, "Security:AdminToken:LifetimeMinutes", DefaultAdminLifetimeMinutes)),
            ObsTokenLifetime = TimeSpan.FromMinutes(ReadLifetimeMinutes(configuration, "Security:ObsToken:LifetimeMinutes", DefaultObsLifetimeMinutes)),
            SigningKey = configuration["Security:AdminToken:SigningKey"]?.Trim() is { Length: > 0 } signingKey ? signingKey : null
        };
    }

    private static IReadOnlyList<string> ReadValues(IConfiguration configuration, string key)
    {
        var section = configuration.GetSection(key);
        var children = section.GetChildren()
            .Select(item => item.Value?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
        if (children.Length > 0) return children;

        return (section.Value ?? "")
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int ReadLifetimeMinutes(IConfiguration configuration, string key, int fallback)
    {
        return int.TryParse(configuration[key], out var configured)
            ? Math.Clamp(configured, 5, 24 * 60)
            : fallback;
    }
}

public sealed class AdminTokenIssuancePolicy
{
    private readonly AdminTokenSecurityOptions _options;

    public AdminTokenIssuancePolicy(AdminTokenSecurityOptions options) => _options = options;

    public bool CanIssue(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (string.Equals(_options.Mode, AdminTokenSecurityOptions.LocalMode, StringComparison.Ordinal))
        {
            // A missing address is used by in-process transports such as TestServer
            // and does not represent a network client.
            return remoteIp is null || IPAddress.IsLoopback(remoteIp) || IsAllowed(remoteIp);
        }

        if (!string.Equals(_options.Mode, AdminTokenSecurityOptions.PublicMode, StringComparison.Ordinal))
        {
            return false;
        }

        return remoteIp is null
            ? _options.AllowedClientIps.Any(entry => string.Equals(entry, "loopback", StringComparison.OrdinalIgnoreCase))
            : IsAllowed(remoteIp);
    }

    private bool IsAllowed(IPAddress address) => _options.AllowedClientIps.Any(entry => Matches(address, entry));

    private static bool Matches(IPAddress address, string entry)
    {
        if (string.Equals(entry, "loopback", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.IsLoopback(address);
        }

        if (entry.Contains('/', StringComparison.Ordinal))
        {
            return MatchesNetwork(address, entry);
        }

        return IPAddress.TryParse(entry, out var allowed)
            && Normalize(address).Equals(Normalize(allowed));
    }

    private static bool MatchesNetwork(IPAddress address, string entry)
    {
        var parts = entry.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefix))
        {
            return false;
        }

        address = Normalize(address);
        network = Normalize(network);
        if (address.AddressFamily != network.AddressFamily) return false;

        var bytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var maxPrefix = bytes.Length * 8;
        if (prefix < 0 || prefix > maxPrefix) return false;

        var fullBytes = prefix / 8;
        var remainingBits = prefix % 8;
        for (var index = 0; index < fullBytes; index++)
        {
            if (bytes[index] != networkBytes[index]) return false;
        }

        if (remainingBits == 0) return true;
        var mask = (byte)(0xff << (8 - remainingBits));
        return (bytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
