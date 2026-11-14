using System.Collections.Concurrent;
using System.Text.Json;

namespace EasyLotteryCardHub.Services;

public sealed record PendingPasskeyCeremony(
    string Kind,
    string OptionsJson,
    DateTimeOffset ExpiresAt,
    string? EmailNormalized = null,
    string? InviteTokenHash = null,
    string? DisplayName = null,
    string? Slug = null,
    Guid? TenantId = null);

// Passkey challenges are short-lived and fail closed after a process restart.
// This store is intentionally local for wave 1; multi-replica deployments need a shared challenge store.
public sealed class PasskeyCeremonyStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, PendingPasskeyCeremony> _pending = new();

    public void Put(Guid id, PendingPasskeyCeremony ceremony)
    {
        PruneExpired();
        _pending[id] = ceremony;
    }

    public bool TryTake(Guid id, out PendingPasskeyCeremony ceremony)
    {
        if (_pending.TryRemove(id, out var pending) && pending.ExpiresAt > timeProvider.GetUtcNow())
        {
            ceremony = pending;
            return true;
        }

        ceremony = null!;
        return false;
    }

    private void PruneExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var item in _pending)
        {
            if (item.Value.ExpiresAt <= now) _pending.TryRemove(item.Key, out _);
        }
    }
}

public sealed class StreamerPasskeyOptions
{
    public required string RpId { get; init; }
    public required string ServerName { get; init; }
    public required HashSet<string> Origins { get; init; }
    public TimeSpan ChallengeLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public Fido2NetLib.Fido2Configuration CreateFido2Configuration() => new()
    {
        RPID = RpId,
        RPName = ServerName,
        Origins = Origins
    };

    public static StreamerPasskeyOptions Load(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Passkey:Origins").GetChildren()
            .Select(item => item.Value?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (origins.Count == 0) origins.UnionWith(["http://localhost:18940", "https://localhost:18940"]);

        return new StreamerPasskeyOptions
        {
            RpId = configuration["Passkey:RpId"]?.Trim() is { Length: > 0 } rpId ? rpId : "localhost",
            ServerName = configuration["Passkey:ServerName"]?.Trim() is { Length: > 0 } name ? name : "EasyLottery Card Hub",
            Origins = origins,
            ChallengeLifetime = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue<int?>("Passkey:ChallengeMinutes") ?? 5, 1, 10))
        };
    }
}
