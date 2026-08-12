using System.Text.Json;
using System.Text.Json.Serialization;
using EasyLotteryInfrastructure.Storage;

namespace EasyLotteryApi.Passkey;

public sealed class PasskeyStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly IReadOnlyDictionary<PasskeyFlow, Func<PasskeyState, PasskeyPendingCeremony?>> PendingReaders = new Dictionary<PasskeyFlow, Func<PasskeyState, PasskeyPendingCeremony?>>
    {
        [PasskeyFlow.Register] = state => state.Registration,
        [PasskeyFlow.Login] = state => state.Authentication,
        [PasskeyFlow.Add] = state => state.Addition
    };
    private static readonly IReadOnlyDictionary<PasskeyFlow, Action<PasskeyState, PasskeyPendingCeremony?>> PendingWriters = new Dictionary<PasskeyFlow, Action<PasskeyState, PasskeyPendingCeremony?>>
    {
        [PasskeyFlow.Register] = (state, pending) => state.Registration = pending,
        [PasskeyFlow.Login] = (state, pending) => state.Authentication = pending,
        [PasskeyFlow.Add] = (state, pending) => state.Addition = pending
    };
    private readonly string _path;
    private readonly IStorageGateProvider _storageGates;

    public PasskeyStateStore(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
    {
        var directory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "admin-passkey.json");
        _storageGates = storageGates;
    }

    public async Task<PasskeyState> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        return await ReadUnsafeAsync(cancellationToken);
    }

    public async Task SetPendingAsync(PasskeyFlow flow, PasskeyPendingCeremony pending, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        if (!PendingWriters.TryGetValue(flow, out var writePending))
            throw new ArgumentOutOfRangeException(nameof(flow), flow, "不支援的 Passkey flow。");
        writePending(state, pending);
        await WriteUnsafeAsync(state, cancellationToken);
    }

    public async Task<PasskeyPendingCeremony?> TakePendingAsync(PasskeyFlow flow, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        if (!PendingReaders.TryGetValue(flow, out var readPending) || !PendingWriters.TryGetValue(flow, out var clearPending))
            throw new ArgumentOutOfRangeException(nameof(flow), flow, "不支援的 Passkey flow。");
        var pending = readPending(state);
        clearPending(state, null);
        await WriteUnsafeAsync(state, cancellationToken);
        return pending is not null && pending.ExpiresAtUtc > DateTimeOffset.UtcNow ? pending : null;
    }

    public async Task SaveCredentialAsync(PasskeyCredentialRecord credential, CancellationToken cancellationToken = default)
        => await AddCredentialAsync(credential, cancellationToken);

    public async Task AddCredentialAsync(PasskeyCredentialRecord credential, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        if (state.Credentials.Any(item => item.CredentialId.SequenceEqual(credential.CredentialId)))
            throw new InvalidOperationException("Admin Passkey 已經註冊。");
        state.Credentials.Add(credential);
        await WriteUnsafeAsync(state, cancellationToken);
    }

    public async Task<bool> UpdateCounterAsync(byte[] credentialId, uint counter, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        var credential = state.Credentials.FirstOrDefault(item => item.CredentialId.SequenceEqual(credentialId));
        if (credential is null) return false;
        credential.SignatureCounter = Math.Max(credential.SignatureCounter, counter);
        await WriteUnsafeAsync(state, cancellationToken);
        return true;
    }

    public async Task<PasskeyRemoveResult> RemoveCredentialAsync(byte[] credentialId, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        var index = state.Credentials.FindIndex(item => item.CredentialId.SequenceEqual(credentialId));
        if (index < 0) return PasskeyRemoveResult.NotFound;
        if (state.Credentials.Count == 1) return PasskeyRemoveResult.LastCredential;

        state.Credentials.RemoveAt(index);
        await WriteUnsafeAsync(state, cancellationToken);
        return PasskeyRemoveResult.Removed;
    }

    private async Task<PasskeyState> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return new PasskeyState();
        await using var stream = File.OpenRead(_path);
        var state = await JsonSerializer.DeserializeAsync<PasskeyState>(stream, JsonOptions, cancellationToken) ?? new PasskeyState();
        state.NormalizeLegacyCredential();
        return state;
    }

    private async Task WriteUnsafeAsync(PasskeyState state, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
        }
        File.Move(temporaryPath, _path, overwrite: true);
    }
}

public sealed class PasskeyState
{
    public List<PasskeyCredentialRecord> Credentials { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("Credential")]
    public PasskeyCredentialRecord? LegacyCredential { get; set; }

    [JsonIgnore]
    public PasskeyCredentialRecord? Credential
    {
        get => Credentials.FirstOrDefault();
        set
        {
            Credentials.Clear();
            if (value is not null) Credentials.Add(value);
        }
    }

    public PasskeyPendingCeremony? Registration { get; set; }
    public PasskeyPendingCeremony? Authentication { get; set; }
    public PasskeyPendingCeremony? Addition { get; set; }

    public void NormalizeLegacyCredential()
    {
        Credentials ??= [];
        if (Credentials.Count == 0 && LegacyCredential is not null)
        {
            Credentials.Add(LegacyCredential);
        }

        LegacyCredential = null;
    }
}

public enum PasskeyRemoveResult
{
    NotFound,
    LastCredential,
    Removed
}

public sealed class PasskeyPendingCeremony
{
    public required string Email { get; init; }
    public required string OptionsJson { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public string? Name { get; init; }
}

public sealed class PasskeyCredentialRecord
{
    public required byte[] CredentialId { get; init; }
    public required byte[] PublicKey { get; init; }
    public required byte[] UserHandle { get; init; }
    public uint SignatureCounter { get; set; }
    public string Name { get; init; } = "Admin Passkey";
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
