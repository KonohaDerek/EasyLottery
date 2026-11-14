using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyLotteryCardHub.Data;
using Npgsql;

namespace EasyLotteryCardHub.Email;

public sealed record InviteEmailPayload(string Recipient, string Subject, string InviteToken, DateTimeOffset ExpiresAtUtc, string StreamerDisplayName);

public interface IInviteEmailSender
{
    Task SendAsync(InviteEmailPayload message, CancellationToken cancellationToken);
}

public sealed class InviteEmailCipher
{
    private readonly byte[] _key;

    public InviteEmailCipher(IConfiguration configuration)
    {
        var encodedKey = configuration["CardHub:EmailOutbox:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(encodedKey))
            throw new InvalidOperationException("CardHub:EmailOutbox:EncryptionKey must be configured before creating invitations.");
        try
        {
            _key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("CardHub:EmailOutbox:EncryptionKey must be base64-encoded.", exception);
        }
        if (_key.Length != 32)
            throw new InvalidOperationException("CardHub:EmailOutbox:EncryptionKey must contain exactly 32 bytes.");
    }

    public byte[] Protect(ReadOnlySpan<byte> cleartext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[cleartext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, cleartext, ciphertext, tag);
        return [.. nonce, .. tag, .. ciphertext];
    }

    public byte[] Unprotect(ReadOnlySpan<byte> protectedPayload)
    {
        if (protectedPayload.Length < 28) throw new CryptographicException("Protected outbox payload is invalid.");
        var nonce = protectedPayload[..12];
        var tag = protectedPayload.Slice(12, 16);
        var ciphertext = protectedPayload[28..];
        var cleartext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, cleartext);
        return cleartext;
    }
}

public sealed class PostgresInviteEmailOutbox(HubDatabase database, InviteEmailCipher cipher)
{
    private NpgsqlDataSource DataSource => database.DataSource ?? throw new InvalidOperationException("Card Hub database is not configured.");

    public async Task EnqueueAsync(Guid inviteId, InviteEmailPayload message, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        var protectedPayload = cipher.Protect(payload);
        await using var command = new NpgsqlCommand("INSERT INTO email_delivery_outbox (id, aggregate_id, message_type, protected_payload) VALUES (@id, @invite_id, 'streamer_invite', @payload)", connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("invite_id", inviteId);
        command.Parameters.AddWithValue("payload", protectedPayload);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InviteEmailPayload>> ReadPendingAsync(CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT protected_payload FROM email_delivery_outbox WHERE message_type = 'streamer_invite' AND delivered_at_utc IS NULL ORDER BY created_at_utc", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var messages = new List<InviteEmailPayload>();
        while (await reader.ReadAsync(cancellationToken))
            messages.Add(JsonSerializer.Deserialize<InviteEmailPayload>(cipher.Unprotect((byte[])reader[0]))
                ?? throw new InvalidDataException("The protected invite outbox payload is invalid."));
        return messages;
    }
}
