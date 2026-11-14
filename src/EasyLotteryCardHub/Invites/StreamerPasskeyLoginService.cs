using System.Security.Cryptography;
using System.Text.Json;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using Fido2NetLib;
using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

namespace EasyLotteryCardHub.Invites;

public sealed record StreamerLoginOptions(Guid ChallengeId, JsonElement Options, DateTimeOffset ExpiresAtUtc);
public sealed record StreamerLoginResult(Guid TenantId, Guid AccountId, string AccessToken, int ExpiresInSeconds);

public sealed class StreamerPasskeyLoginService(
    HubDatabase database,
    SiteAccessTokenIssuer tokens,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    TimeProvider timeProvider)
{
    private NpgsqlDataSource DataSource => database.DataSource ?? throw new InvalidOperationException("Card Hub database is not configured.");

    public async Task<StreamerLoginOptions> CreateOptionsAsync(CancellationToken cancellationToken)
    {
        var options = PasskeyConfiguration.CreateDiscoverableAssertionOptions(configuration, environment.IsDevelopment());
        var challengeId = Guid.NewGuid();
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(5);
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var save = new NpgsqlCommand("INSERT INTO streamer_passkey_login_challenges (id, options_json, expires_at_utc) VALUES (@id, @options, @expires)", connection);
        save.Parameters.AddWithValue("id", challengeId);
        save.Parameters.AddWithValue("options", options.ToJson());
        save.Parameters.AddWithValue("expires", expiresAt);
        await save.ExecuteNonQueryAsync(cancellationToken);
        return new StreamerLoginOptions(challengeId, JsonDocument.Parse(options.ToJson()).RootElement.Clone(), expiresAt);
    }

    public async Task<StreamerLoginResult?> VerifyAsync(Guid challengeId, JsonElement credential, CancellationToken cancellationToken)
    {
        if (credential.ValueKind != JsonValueKind.Object
            || !credential.TryGetProperty("rawId", out var rawIdElement)
            || string.IsNullOrWhiteSpace(rawIdElement.GetString())) return null;
        byte[] credentialId;
        try { credentialId = WebEncoders.Base64UrlDecode(rawIdElement.GetString()!); }
        catch (FormatException) { return null; }
        Guid accountId;
        Guid tenantId;
        string optionsJson;
        byte[] publicKey;
        byte[] userHandle;
        uint signCount;
        await using (var connection = await DataSource.OpenConnectionAsync(cancellationToken))
        await using (var query = new NpgsqlCommand("SELECT c.options_json, a.id, a.tenant_id, a.passkey_public_key, a.passkey_user_handle, a.passkey_signature_count FROM streamer_passkey_login_challenges c JOIN streamer_accounts a ON a.passkey_credential_id = @credential_id JOIN streamers t ON t.id = a.tenant_id WHERE c.id = @id AND c.consumed_at_utc IS NULL AND c.expires_at_utc > @now AND t.status = 'active'", connection))
        {
            query.Parameters.AddWithValue("credential_id", credentialId);
            query.Parameters.AddWithValue("id", challengeId);
            query.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            optionsJson = reader.GetString(0);
            accountId = reader.GetGuid(1);
            tenantId = reader.GetGuid(2);
            publicKey = (byte[])reader[3];
            userHandle = (byte[])reader[4];
            signCount = checked((uint)reader.GetInt64(5));
        }

        try
        {
            var response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(credential.GetRawText());
            if (response is null) return null;
            var result = await new Fido2(PasskeyConfiguration.Create(configuration, environment.IsDevelopment())).MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = response,
                OriginalOptions = AssertionOptions.FromJson(optionsJson),
                StoredPublicKey = publicKey,
                StoredSignatureCounter = signCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                    args.CredentialId.SequenceEqual(credentialId) && args.UserHandle.SequenceEqual(userHandle))
            }, cancellationToken);

            await using var completion = await DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await completion.BeginTransactionAsync(cancellationToken);
            await using (var consume = new NpgsqlCommand("UPDATE streamer_passkey_login_challenges SET consumed_at_utc = @now WHERE id = @id AND consumed_at_utc IS NULL AND expires_at_utc > @now", completion, transaction))
            {
                consume.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
                consume.Parameters.AddWithValue("id", challengeId);
                if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1) return null;
            }
            await using (var update = new NpgsqlCommand("UPDATE streamer_accounts SET passkey_signature_count = GREATEST(passkey_signature_count, @count) WHERE id = @account_id", completion, transaction))
            {
                update.Parameters.AddWithValue("count", (long)result.SignCount);
                update.Parameters.AddWithValue("account_id", accountId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            var (token, expiry) = tokens.IssueStreamer(tenantId, accountId);
            return new StreamerLoginResult(tenantId, accountId, token, expiry);
        }
        catch (Exception exception) when (exception is Fido2VerificationException or JsonException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }
}
