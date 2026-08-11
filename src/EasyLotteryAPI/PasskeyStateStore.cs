using System.Text.Json;
using EasyLotteryInfrastructure.Storage;

namespace EasyLotteryApi;

public sealed class PasskeyStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
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

    public async Task SetPendingAsync(string flow, PasskeyPendingCeremony pending, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        if (flow == AdminPasskeyOptions.RegisterFlow) state.Registration = pending;
        else state.Authentication = pending;
        await WriteUnsafeAsync(state, cancellationToken);
    }

    public async Task<PasskeyPendingCeremony?> TakePendingAsync(string flow, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        var pending = flow == AdminPasskeyOptions.RegisterFlow ? state.Registration : state.Authentication;
        if (flow == AdminPasskeyOptions.RegisterFlow) state.Registration = null;
        else state.Authentication = null;
        await WriteUnsafeAsync(state, cancellationToken);
        return pending is not null && pending.ExpiresAtUtc > DateTimeOffset.UtcNow ? pending : null;
    }

    public async Task SaveCredentialAsync(PasskeyCredentialRecord credential, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        if (state.Credential is not null) throw new InvalidOperationException("Admin Passkey 已經註冊。");
        state.Credential = credential;
        await WriteUnsafeAsync(state, cancellationToken);
    }

    public async Task<bool> UpdateCounterAsync(byte[] credentialId, uint counter, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _path);
        var state = await ReadUnsafeAsync(cancellationToken);
        if (state.Credential is null || !state.Credential.CredentialId.SequenceEqual(credentialId)) return false;
        state.Credential.SignatureCounter = Math.Max(state.Credential.SignatureCounter, counter);
        await WriteUnsafeAsync(state, cancellationToken);
        return true;
    }

    private async Task<PasskeyState> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return new PasskeyState();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<PasskeyState>(stream, JsonOptions, cancellationToken) ?? new PasskeyState();
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
    public PasskeyCredentialRecord? Credential { get; set; }
    public PasskeyPendingCeremony? Registration { get; set; }
    public PasskeyPendingCeremony? Authentication { get; set; }
}

public sealed class PasskeyPendingCeremony
{
    public required string Email { get; init; }
    public required string OptionsJson { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
}

public sealed class PasskeyCredentialRecord
{
    public required byte[] CredentialId { get; init; }
    public required byte[] PublicKey { get; init; }
    public required byte[] UserHandle { get; init; }
    public uint SignatureCounter { get; set; }
}
