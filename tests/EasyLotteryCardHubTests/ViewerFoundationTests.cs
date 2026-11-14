using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Email;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Viewers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EasyLotteryCardHubTests;

[TestClass]
public sealed class ViewerFoundationTests
{
    [TestMethod]
    public async Task ViewerCodesRefreshGroupsExportAndDeletionRespectAccountBoundaries()
    {
        var baseConnection = Environment.GetEnvironmentVariable("CARD_HUB_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnection)) Assert.Inconclusive("CARD_HUB_TEST_CONNECTION is not configured.");
        var schema = "cardhub_viewer_" + Guid.NewGuid().ToString("N");
        await using var bootstrap = NpgsqlDataSource.Create(baseConnection);
        await ExecuteAsync(bootstrap, $"CREATE SCHEMA \"{schema}\"");
        var scoped = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema };
        await using var dataSource = NpgsqlDataSource.Create(scoped.ConnectionString);
        try
        {
            await new HubDatabaseMigrator(new HubDatabase(dataSource), NullLogger<HubDatabaseMigrator>.Instance).StartAsync(CancellationToken.None);
            await ExecuteAsync(dataSource, "CREATE TABLE card_instances (id uuid PRIMARY KEY, card_id uuid NOT NULL, owner_viewer_id uuid NULL, status varchar(16) NOT NULL, origin varchar(16) NOT NULL)");
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CardHub:Identity:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["CardHub:EmailOutbox:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["CardHub:SiteToken:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["CardHub:Viewer:TermsVersion"] = "terms-test-v1"
            }).Build();
            var database = new HubDatabase(dataSource);
            var hasher = new IdentityHasher(config);
            var cipher = new InviteEmailCipher(config);
            var tokenIssuer = new SiteAccessTokenIssuer(config, TimeProvider.System);
            var viewers = new ViewerAccountsService(database, hasher, new ViewerEmailOutbox(cipher), tokenIssuer, TimeProvider.System, config);

            var aliceEmail = "alice@example.test";
            await viewers.RequestCodeAsync(new ViewerCodeRequest(aliceEmail, "13_17", null), "192.0.2.11", CancellationToken.None);
            var aliceCode = await ReadQueuedCodeAsync(dataSource, cipher, aliceEmail);
            await using (var checkCode = await dataSource.OpenConnectionAsync())
            await using (var check = new NpgsqlCommand("SELECT code_hash FROM viewer_login_codes WHERE email_hash = @email", checkCode))
            {
                check.Parameters.AddWithValue("email", hasher.HashEmail(aliceEmail));
                var codeHash = (byte[])(await check.ExecuteScalarAsync() ?? throw new AssertFailedException("Code row missing."));
                CollectionAssert.AreNotEqual(Encoding.UTF8.GetBytes(aliceCode.Code), codeHash);
            }

            var alice = await viewers.VerifyCodeAsync(new ViewerCodeVerification(aliceEmail, aliceCode.Code, true, "terms-test-v1"), CancellationToken.None);
            Assert.IsTrue(tokenIssuer.TryValidateViewerToken(alice.AccessToken, out var aliceId, out var ageBracket));
            Assert.AreEqual(alice.ViewerId, aliceId);
            Assert.AreEqual("13_17", ageBracket);
            Assert.IsFalse(tokenIssuer.TryValidateStreamerToken(alice.AccessToken, out _, out _));
            Assert.IsFalse(tokenIssuer.TryValidateSiteToken(alice.AccessToken, out _, out _, out _));

            await viewers.SetHandleAsync(alice.ViewerId, "Alice_13", CancellationToken.None);
            await Assert.ThrowsExactlyAsync<ViewerServiceException>(() => viewers.SetHandleAsync(alice.ViewerId, "Admin", CancellationToken.None));
            var group = await viewers.CreateGroupAsync(alice.ViewerId, null, "My collection", CancellationToken.None);
            var aliceCard = Guid.NewGuid();
            var bobCard = Guid.NewGuid();
            await InsertCardAsync(dataSource, aliceCard, alice.ViewerId);

            var bobEmail = "bob@example.test";
            await viewers.RequestCodeAsync(new ViewerCodeRequest(bobEmail, "18_plus", null), "192.0.2.12", CancellationToken.None);
            var bobCode = await ReadQueuedCodeAsync(dataSource, cipher, bobEmail);
            var bob = await viewers.VerifyCodeAsync(new ViewerCodeVerification(bobEmail, bobCode.Code, true, "terms-test-v1"), CancellationToken.None);
            await InsertCardAsync(dataSource, bobCard, bob.ViewerId);

            await viewers.AddCardToGroupAsync(alice.ViewerId, group.Id, aliceCard, CancellationToken.None);
            var foreignCard = await Assert.ThrowsExactlyAsync<ViewerServiceException>(() => viewers.AddCardToGroupAsync(alice.ViewerId, group.Id, bobCard, CancellationToken.None));
            Assert.AreEqual(StatusCodes.Status404NotFound, foreignCard.StatusCode);
            var exported = await viewers.ExportAsync(alice.ViewerId, CancellationToken.None);
            Assert.AreEqual("13_17", exported.AgeBracket);
            Assert.AreEqual(aliceCard, exported.Collection.Single().GetProperty("id").GetGuid());
            Assert.IsTrue(exported.Omissions.Any(item => item.Contains("email address", StringComparison.Ordinal)));
            await using (var auditConnection = await dataSource.OpenConnectionAsync())
            await using (var audit = new NpgsqlCommand("SELECT count(*) FROM audit_events WHERE event_type = 'viewer.data_exported' AND actor_id = @id", auditConnection))
            {
                audit.Parameters.AddWithValue("id", alice.ViewerId.ToString("D"));
                Assert.AreEqual(1L, (long)(await audit.ExecuteScalarAsync() ?? 0L));
            }

            var rotated = await viewers.RefreshAsync(alice.RefreshToken, CancellationToken.None);
            Assert.AreNotEqual(alice.RefreshToken, rotated.RefreshToken);
            var reuse = await Assert.ThrowsExactlyAsync<ViewerServiceException>(() => viewers.RefreshAsync(alice.RefreshToken, CancellationToken.None));
            Assert.AreEqual(StatusCodes.Status401Unauthorized, reuse.StatusCode);
            await Assert.ThrowsExactlyAsync<ViewerServiceException>(() => viewers.RefreshAsync(rotated.RefreshToken, CancellationToken.None));

            await viewers.DeleteAccountAsync(alice.ViewerId, CancellationToken.None);
            Assert.IsFalse(await viewers.IsActiveAsync(alice.ViewerId, CancellationToken.None));
            var deletedViewerGroup = await Assert.ThrowsExactlyAsync<ViewerServiceException>(() =>
                viewers.CreateGroupAsync(alice.ViewerId, null, "Must not recreate", CancellationToken.None));
            Assert.AreEqual(StatusCodes.Status401Unauthorized, deletedViewerGroup.StatusCode);
            await using (var verifyDeletion = await dataSource.OpenConnectionAsync())
            {
                await using var account = new NpgsqlCommand("SELECT deleted_at_utc IS NOT NULL AND email_hash <> @email FROM viewers WHERE id = @viewer", verifyDeletion);
                account.Parameters.AddWithValue("email", hasher.HashEmail(aliceEmail));
                account.Parameters.AddWithValue("viewer", alice.ViewerId);
                Assert.AreEqual(true, await account.ExecuteScalarAsync());
                await using var sessions = new NpgsqlCommand("SELECT count(*) FROM viewer_refresh_tokens WHERE viewer_id = @viewer", verifyDeletion);
                sessions.Parameters.AddWithValue("viewer", alice.ViewerId);
                Assert.AreEqual(0L, (long)(await sessions.ExecuteScalarAsync() ?? 0L));
                await using var card = new NpgsqlCommand("SELECT owner_viewer_id IS NULL AND status = 'retired' FROM card_instances WHERE id = @card", verifyDeletion);
                card.Parameters.AddWithValue("card", aliceCard);
                Assert.AreEqual(true, await card.ExecuteScalarAsync());
                await using var groupCount = new NpgsqlCommand("SELECT count(*) FROM viewer_groups WHERE viewer_id = @viewer", verifyDeletion);
                groupCount.Parameters.AddWithValue("viewer", alice.ViewerId);
                Assert.AreEqual(0L, (long)(await groupCount.ExecuteScalarAsync() ?? 0L));
            }
        }
        finally
        {
            await ExecuteAsync(bootstrap, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    [TestMethod]
    public async Task ViewerCodeInvalidatesAfterFiveWrongAttempts()
    {
        var baseConnection = Environment.GetEnvironmentVariable("CARD_HUB_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnection)) Assert.Inconclusive("CARD_HUB_TEST_CONNECTION is not configured.");
        var schema = "cardhub_viewer_attempts_" + Guid.NewGuid().ToString("N");
        await using var bootstrap = NpgsqlDataSource.Create(baseConnection);
        await ExecuteAsync(bootstrap, $"CREATE SCHEMA \"{schema}\"");
        var scoped = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema };
        await using var dataSource = NpgsqlDataSource.Create(scoped.ConnectionString);
        try
        {
            await new HubDatabaseMigrator(new HubDatabase(dataSource), NullLogger<HubDatabaseMigrator>.Instance).StartAsync(CancellationToken.None);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CardHub:Identity:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["CardHub:EmailOutbox:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["CardHub:SiteToken:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["CardHub:Viewer:TermsVersion"] = "terms-test-v1"
            }).Build();
            var service = new ViewerAccountsService(new HubDatabase(dataSource), new IdentityHasher(config),
                new ViewerEmailOutbox(new InviteEmailCipher(config)), new SiteAccessTokenIssuer(config, TimeProvider.System), TimeProvider.System, config);
            var email = "attempts@example.test";
            await service.RequestCodeAsync(new ViewerCodeRequest(email, "18_plus", null), "192.0.2.20", CancellationToken.None);
            var correctCode = await ReadQueuedCodeAsync(dataSource, new InviteEmailCipher(config), email);
            var wrongCode = correctCode.Code == "000000" ? "999999" : "000000";
            var emailHash = new IdentityHasher(config).HashEmail(email);
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var exception = await Assert.ThrowsExactlyAsync<ViewerServiceException>(() =>
                    service.VerifyCodeAsync(new ViewerCodeVerification(email, wrongCode, true, "terms-test-v1"), CancellationToken.None));
                Assert.AreEqual(attempt == 5 ? "viewer_code_too_many_attempts" : "viewer_code_wrong", exception.Code);
            }
            var blocked = await Assert.ThrowsExactlyAsync<ViewerServiceException>(() =>
                service.VerifyCodeAsync(new ViewerCodeVerification(email, wrongCode, true, "terms-test-v1"), CancellationToken.None));
            Assert.AreEqual(StatusCodes.Status429TooManyRequests, blocked.StatusCode);
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var row = new NpgsqlCommand("SELECT attempt_count, consumed_at_utc IS NOT NULL FROM viewer_login_codes WHERE email_hash = @hash", connection);
            row.Parameters.AddWithValue("hash", emailHash);
            await using var reader = await row.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual(5, reader.GetInt32(0));
            Assert.IsTrue(reader.GetBoolean(1));
        }
        finally
        {
            await ExecuteAsync(bootstrap, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static async Task<ViewerLoginEmail> ReadQueuedCodeAsync(NpgsqlDataSource dataSource, InviteEmailCipher cipher, string email)
    {
        // Tests query the row by clear code recipient only after decrypting; the address exists solely in ciphertext.
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT protected_payload FROM viewer_email_outbox ORDER BY created_at_utc", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var payload = (byte[])reader[0];
            var message = JsonSerializer.Deserialize<ViewerLoginEmail>(cipher.Unprotect(payload))
                ?? throw new AssertFailedException("Encrypted login email payload invalid.");
            if (!string.Equals(message.Recipient, email, StringComparison.Ordinal)) continue;
            Assert.IsFalse(Encoding.UTF8.GetString(payload).Contains(email, StringComparison.Ordinal));
            return message;
        }
        throw new AssertFailedException("Encrypted login email missing.");
    }

    private static async Task InsertCardAsync(NpgsqlDataSource dataSource, Guid cardId, Guid viewerId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var insert = new NpgsqlCommand("INSERT INTO card_instances (id, card_id, owner_viewer_id, status, origin) VALUES (@id, @card, @viewer, 'owned', 'pack')", connection);
        insert.Parameters.AddWithValue("id", cardId);
        insert.Parameters.AddWithValue("card", Guid.NewGuid());
        insert.Parameters.AddWithValue("viewer", viewerId);
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(NpgsqlDataSource source, string sql)
    {
        await using var connection = await source.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
