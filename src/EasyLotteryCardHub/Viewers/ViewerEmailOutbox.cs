using System.Text.Json;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Email;
using Npgsql;

namespace EasyLotteryCardHub.Viewers;

public sealed record ViewerLoginEmail(string Recipient, string Subject, string Body, string Code, DateTimeOffset ExpiresAtUtc);

public sealed class ViewerEmailOutbox(InviteEmailCipher cipher)
{
    public async Task EnqueueAsync(Guid loginCodeId, ViewerLoginEmail email, NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(email);
        var encrypted = cipher.Protect(json);
        await using var command = new NpgsqlCommand("INSERT INTO viewer_email_outbox (id, login_code_id, protected_payload) VALUES (@id, @code_id, @payload)", connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("code_id", loginCodeId);
        command.Parameters.AddWithValue("payload", encrypted);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
