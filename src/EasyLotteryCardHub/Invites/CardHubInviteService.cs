using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Email;
using EasyLotteryCardHub.Endpoints;
using EasyLotteryCardHub.Security;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Hosting;
using Npgsql;

namespace EasyLotteryCardHub.Invites;

public sealed record CreateStreamerInviteRequest(string Email, string Slug, string DisplayName, int? ExpiresInHours);
public sealed record StreamerInviteCreated(Guid Id, DateTimeOffset ExpiresAtUtc, string DeliveryStatus);
public sealed record PasskeyRegistrationOptions(JsonElement Options, DateTimeOffset ExpiresAtUtc);
public sealed record StreamerRegistrationResult(Guid TenantId, Guid AccountId, string AccessToken, int ExpiresInSeconds);

public sealed class CardHubInviteService(
    HubDatabase database,
    PostgresInviteEmailOutbox emailOutbox,
    SiteAccessTokenIssuer tokens,
    IdentityHasher identityHasher,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    TimeProvider timeProvider)
{
    private NpgsqlDataSource DataSource => database.DataSource ?? throw new InvalidOperationException("Card Hub database is not configured.");

    public async Task<StreamerInviteCreated> CreateAsync(CreateStreamerInviteRequest request, string createdBy, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (!AdminEndpoints.IsValidSlug(request.Slug) || string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120)
            throw new ArgumentException("A valid streamer slug and display name are required.");

        var id = Guid.NewGuid();
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiresAt = timeProvider.GetUtcNow().AddHours(Math.Clamp(request.ExpiresInHours ?? 72, 1, 24 * 14));
        var emailHash = identityHasher.HashEmail(email);
        var inviteHash = SiteSecret.Hash(token);
        var message = new InviteEmailPayload(email, "你受邀加入 EasyLottery Card Hub 測試", token, expiresAt, request.DisplayName.Trim());

        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var duplicate = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM streamers WHERE slug = @slug) OR EXISTS (SELECT 1 FROM streamer_invites WHERE streamer_slug = @slug AND redeemed_at_utc IS NULL AND expires_at_utc > @now)", connection, transaction))
        {
            duplicate.Parameters.AddWithValue("slug", request.Slug);
            duplicate.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
            if ((bool)(await duplicate.ExecuteScalarAsync(cancellationToken) ?? true))
                throw new InvalidOperationException("A streamer with this slug is already registered or invited.");
        }

        await using (var insert = new NpgsqlCommand("INSERT INTO streamer_invites (id, streamer_slug, streamer_display_name, recipient_email_hash, invite_token_hash, expires_at_utc, created_by) VALUES (@id, @slug, @name, @email_hash, @token_hash, @expires, @created_by)", connection, transaction))
        {
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("slug", request.Slug);
            insert.Parameters.AddWithValue("name", request.DisplayName.Trim());
            insert.Parameters.AddWithValue("email_hash", emailHash);
            insert.Parameters.AddWithValue("token_hash", inviteHash);
            insert.Parameters.AddWithValue("expires", expiresAt);
            insert.Parameters.AddWithValue("created_by", createdBy);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        // The outbox stores an authenticated-encrypted payload; plaintext email/token are never written to logs or columns.
        await emailOutbox.EnqueueAsync(id, message, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new StreamerInviteCreated(id, expiresAt, "queued");
    }

    public async Task<PasskeyRegistrationOptions?> GetRegistrationOptionsAsync(string inviteToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inviteToken) || inviteToken.Length > 200) return null;
        var inviteHash = SiteSecret.Hash(inviteToken);
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var lookup = new NpgsqlCommand("SELECT id, streamer_slug, streamer_display_name, registration_options_json, registration_options_expires_at_utc FROM streamer_invites WHERE invite_token_hash = @hash AND redeemed_at_utc IS NULL AND expires_at_utc > @now", connection);
        lookup.Parameters.AddWithValue("hash", inviteHash);
        lookup.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var inviteId = reader.GetGuid(0);
        var slug = reader.GetString(1);
        var displayName = reader.GetString(2);
        var savedJson = reader.IsDBNull(3) ? null : reader.GetString(3);
        DateTimeOffset? savedExpiry = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4);
        await reader.CloseAsync();

        var now = timeProvider.GetUtcNow();
        if (savedJson is not null && savedExpiry > now)
            return new PasskeyRegistrationOptions(JsonDocument.Parse(savedJson).RootElement.Clone(), savedExpiry.Value);

        var optionsExpiry = now.AddMinutes(5);
        var optionsJson = CreateRegistrationOptions(inviteId, slug, displayName);
        await using var save = new NpgsqlCommand("UPDATE streamer_invites SET registration_options_json = @options, registration_options_expires_at_utc = @options_expires WHERE id = @id AND invite_token_hash = @hash AND redeemed_at_utc IS NULL AND expires_at_utc > @now", connection);
        save.Parameters.AddWithValue("options", optionsJson);
        save.Parameters.AddWithValue("options_expires", optionsExpiry);
        save.Parameters.AddWithValue("id", inviteId);
        save.Parameters.AddWithValue("hash", inviteHash);
        save.Parameters.AddWithValue("now", now);
        if (await save.ExecuteNonQueryAsync(cancellationToken) != 1) return null;
        return new PasskeyRegistrationOptions(JsonDocument.Parse(optionsJson).RootElement.Clone(), optionsExpiry);
    }

    public async Task<StreamerRegistrationResult?> CompleteRegistrationAsync(string inviteToken, JsonElement credential, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inviteToken) || inviteToken.Length > 200 || credential.ValueKind != JsonValueKind.Object) return null;
        var inviteHash = SiteSecret.Hash(inviteToken);
        Guid inviteId;
        string optionsJson;
        byte[] emailHash;
        await using (var connection = await DataSource.OpenConnectionAsync(cancellationToken))
        await using (var lookup = new NpgsqlCommand("SELECT id, registration_options_json, recipient_email_hash FROM streamer_invites WHERE invite_token_hash = @hash AND redeemed_at_utc IS NULL AND expires_at_utc > @now AND registration_options_expires_at_utc > @now", connection))
        {
            lookup.Parameters.AddWithValue("hash", inviteHash);
            lookup.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
            await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(1)) return null;
            inviteId = reader.GetGuid(0);
            optionsJson = reader.GetString(1);
            emailHash = (byte[])reader[2];
        }

        (byte[] Id, byte[] PublicKey, uint SignCount) credentialResult;
        try
        {
            credentialResult = await VerifyNewCredentialAsync(optionsJson, credential, cancellationToken);
            if (credentialResult.Id.Length == 0) return null;
        }
        catch (Exception exception) when (exception is Fido2VerificationException or JsonException or FormatException)
        {
            return null;
        }

        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userHandle = SHA256.HashData(inviteId.ToByteArray());
        var (accessToken, expiresIn) = tokens.IssueStreamer(tenantId, accountId);
        var now = timeProvider.GetUtcNow();
        await using var completionConnection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await completionConnection.BeginTransactionAsync(cancellationToken);
        string slug;
        string displayName;
        await using (var consume = new NpgsqlCommand("UPDATE streamer_invites SET redeemed_at_utc = @now WHERE id = @id AND invite_token_hash = @hash AND redeemed_at_utc IS NULL AND expires_at_utc > @now AND registration_options_expires_at_utc > @now RETURNING streamer_slug, streamer_display_name", completionConnection, transaction))
        {
            consume.Parameters.AddWithValue("now", now);
            consume.Parameters.AddWithValue("id", inviteId);
            consume.Parameters.AddWithValue("hash", inviteHash);
            await using var reader = await consume.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            slug = reader.GetString(0);
            displayName = reader.GetString(1);
        }

        await using (var addTenant = new NpgsqlCommand("INSERT INTO streamers (id, slug, display_name, status) VALUES (@id, @slug, @name, 'active')", completionConnection, transaction))
        {
            addTenant.Parameters.AddWithValue("id", tenantId);
            addTenant.Parameters.AddWithValue("slug", slug);
            addTenant.Parameters.AddWithValue("name", displayName);
            await addTenant.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var addAccount = new NpgsqlCommand("INSERT INTO streamer_accounts (id, tenant_id, email_hash, passkey_credential_id, passkey_public_key, passkey_user_handle, passkey_signature_count) VALUES (@id, @tenant, @email, @credential_id, @public_key, @handle, @count)", completionConnection, transaction))
        {
            addAccount.Parameters.AddWithValue("id", accountId);
            addAccount.Parameters.AddWithValue("tenant", tenantId);
            addAccount.Parameters.AddWithValue("email", emailHash);
            addAccount.Parameters.AddWithValue("credential_id", credentialResult.Id);
            addAccount.Parameters.AddWithValue("public_key", credentialResult.PublicKey);
            addAccount.Parameters.AddWithValue("handle", userHandle);
            addAccount.Parameters.AddWithValue("count", (long)credentialResult.SignCount);
            await addAccount.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var audit = new NpgsqlCommand("INSERT INTO audit_events (id, tenant_id, actor_type, actor_id, event_type, subject_type, subject_id) VALUES (@id, @tenant, 'streamer', @actor, 'streamer.registered', 'streamer', @subject)", completionConnection, transaction))
        {
            audit.Parameters.AddWithValue("id", Guid.NewGuid());
            audit.Parameters.AddWithValue("tenant", tenantId);
            audit.Parameters.AddWithValue("actor", accountId.ToString("D"));
            audit.Parameters.AddWithValue("subject", tenantId.ToString("D"));
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return new StreamerRegistrationResult(tenantId, accountId, accessToken, expiresIn);
    }

    private string CreateRegistrationOptions(Guid inviteId, string slug, string displayName)
    {
        return PasskeyConfiguration.CreateResidentCredentialOptions(configuration, environment.IsDevelopment(), inviteId, slug, displayName).ToJson();
    }

    private async Task<(byte[] Id, byte[] PublicKey, uint SignCount)> VerifyNewCredentialAsync(string optionsJson, JsonElement credential, CancellationToken cancellationToken)
    {
        var attestation = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(credential.GetRawText())
            ?? throw new JsonException("Passkey attestation is missing.");
        var fido = new Fido2(PasskeyConfiguration.Create(configuration, environment.IsDevelopment()));
        var result = await fido.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestation,
            OriginalOptions = CredentialCreateOptions.FromJson(optionsJson),
            IsCredentialIdUniqueToUserCallback = async (args, token) => await IsCredentialIdUniqueAsync(args.CredentialId, token)
        }, cancellationToken);
        return (result.Id, result.PublicKey, result.SignCount);
    }

    private async Task<bool> IsCredentialIdUniqueAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT NOT EXISTS (SELECT 1 FROM streamer_accounts WHERE passkey_credential_id = @id)", connection);
        command.Parameters.AddWithValue("id", credentialId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        if (normalized.Length > 254 || !MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
            throw new ArgumentException("A valid recipient email is required.");
        return normalized;
    }
}
