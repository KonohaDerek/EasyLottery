using System.Security.Cryptography;
using System.Text.Json;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Email;
using EasyLotteryCardHub.Invites;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EasyLotteryCardHubTests;

[TestClass]
public sealed class PostgresFoundationTests
{
    [TestMethod]
    public async Task MigrationCredentialRotationRevocationAndTenantDisablement()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("CARD_HUB_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) Assert.Inconclusive("CARD_HUB_TEST_CONNECTION is not configured.");

        var schema = "cardhub_test_" + Guid.NewGuid().ToString("N");
        await using var bootstrap = NpgsqlDataSource.Create(baseConnectionString);
        await ExecuteAsync(bootstrap, $"CREATE SCHEMA \"{schema}\"");
        var scopedBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schema };
        await using var dataSource = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try
        {
            var migrator = new HubDatabaseMigrator(new HubDatabase(dataSource), NullLogger<HubDatabaseMigrator>.Instance);
            await Task.WhenAll(migrator.StartAsync(CancellationToken.None), migrator.StartAsync(CancellationToken.None));
            await migrator.StartAsync(CancellationToken.None);
            var store = new PostgresCardHubStore(new HubDatabase(dataSource), TimeProvider.System);
            var streamer = await store.CreateStreamerAsync("integration-streamer", "Integration Streamer", CancellationToken.None);
            var site = await store.CreateSiteAsync(streamer.Id, "Integration Site", "https://lottery.example.test", CancellationToken.None);
            var initial = await store.CreateCredentialAsync(site.Id, CancellationToken.None);
            Assert.IsNotNull(await store.AuthenticateSiteAsync(initial.ClientId, initial.ClientSecret, CancellationToken.None));
            Assert.IsNull(await store.AuthenticateSiteAsync(initial.ClientId, "wrong-secret", CancellationToken.None));

            var rotated = await store.RotateCredentialAsync(site.Id, CancellationToken.None, streamer.Id);
            Assert.IsNotNull(await store.AuthenticateSiteAsync(initial.ClientId, initial.ClientSecret, CancellationToken.None), "The previous secret should work during the overlap window.");
            Assert.IsNotNull(await store.AuthenticateSiteAsync(rotated.ClientId, rotated.ClientSecret, CancellationToken.None));
            await store.RevokeCredentialAsync(site.Id, initial.Id, CancellationToken.None, streamer.Id);
            Assert.IsNull(await store.AuthenticateSiteAsync(initial.ClientId, initial.ClientSecret, CancellationToken.None));

            Assert.IsTrue(await store.SetTenantStatusAsync(streamer.Id, TenantStatus.Disabled, CancellationToken.None));
            Assert.IsNull(await store.AuthenticateSiteAsync(rotated.ClientId, rotated.ClientSecret, CancellationToken.None));
            Assert.IsFalse(await store.SetTenantStatusAsync(streamer.Id, TenantStatus.Deleted, CancellationToken.None), "Deletion must not skip pending_deletion.");
            Assert.IsTrue(await store.SetTenantStatusAsync(streamer.Id, TenantStatus.PendingDeletion, CancellationToken.None));
            Assert.IsFalse(await store.SetTenantStatusAsync(streamer.Id, TenantStatus.Deleted, CancellationToken.None), "Deletion must wait the full 30-day period.");
            await using (var ageConnection = await dataSource.OpenConnectionAsync())
            await using (var agePending = new NpgsqlCommand("UPDATE streamers SET pending_deletion_at_utc = now() - interval '31 days' WHERE id = @id", ageConnection))
            {
                agePending.Parameters.AddWithValue("id", streamer.Id);
                await agePending.ExecuteNonQueryAsync();
            }
            Assert.IsTrue(await store.SetTenantStatusAsync(streamer.Id, TenantStatus.Deleted, CancellationToken.None));

            await using var connection = await dataSource.OpenConnectionAsync();
            await using var tableCheck = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = current_schema()", connection);
            Assert.IsTrue(Convert.ToInt32(await tableCheck.ExecuteScalarAsync()) >= 9);
        }
        finally
        {
            await ExecuteAsync(bootstrap, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    [TestMethod]
    public async Task InviteOutboxIsEncryptedAndExpiredOrRedeemedInvitesCannotStartPasskeyEnrollment()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("CARD_HUB_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) Assert.Inconclusive("CARD_HUB_TEST_CONNECTION is not configured.");

        var schema = "cardhub_invite_test_" + Guid.NewGuid().ToString("N");
        await using var bootstrap = NpgsqlDataSource.Create(baseConnectionString);
        await ExecuteAsync(bootstrap, $"CREATE SCHEMA \"{schema}\"");
        var scopedBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schema };
        await using var dataSource = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try
        {
            await new HubDatabaseMigrator(new HubDatabase(dataSource), NullLogger<HubDatabaseMigrator>.Instance).StartAsync(CancellationToken.None);
            var testKey = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
            var siteKey = RandomNumberGenerator.GetBytes(32);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CardHub:EmailOutbox:EncryptionKey"] = Convert.ToBase64String(testKey),
                ["CardHub:SiteToken:SigningKey"] = Convert.ToBase64String(siteKey),
                ["CardHub:Identity:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["CardHub:Passkey:RpId"] = "localhost",
                ["CardHub:Passkey:Origins:0"] = "http://localhost:8080"
            }).Build();
            var database = new HubDatabase(dataSource);
            var cipher = new InviteEmailCipher(configuration);
            var outbox = new PostgresInviteEmailOutbox(database, cipher);
            var issuer = new SiteAccessTokenIssuer(configuration, TimeProvider.System);
            var webEnvironment = WebApplication.CreateBuilder().Environment;
            var invites = new CardHubInviteService(database, outbox, issuer, new IdentityHasher(configuration), configuration, webEnvironment, TimeProvider.System);
            var created = await invites.CreateAsync(new CreateStreamerInviteRequest("privacy@example.test", "invite-test", "Invite Test", 12), "integration-test", CancellationToken.None);
            var queued = (await outbox.ReadPendingAsync(CancellationToken.None)).Single();
            Assert.AreEqual("privacy@example.test", queued.Recipient);
            Assert.IsNotNull(await invites.GetRegistrationOptionsAsync(queued.InviteToken, CancellationToken.None));

            await using var connection = await dataSource.OpenConnectionAsync();
            await using (var payloadQuery = new NpgsqlCommand("SELECT protected_payload FROM email_delivery_outbox WHERE aggregate_id = @id", connection))
            {
                payloadQuery.Parameters.AddWithValue("id", created.Id);
                var bytes = (byte[])(await payloadQuery.ExecuteScalarAsync() ?? throw new AssertFailedException("Invite outbox record missing."));
                var encoded = System.Text.Encoding.UTF8.GetString(bytes);
                Assert.IsFalse(encoded.Contains(queued.Recipient, StringComparison.Ordinal));
                Assert.IsFalse(encoded.Contains(queued.InviteToken, StringComparison.Ordinal));
            }

            await using (var markExpired = new NpgsqlCommand("UPDATE streamer_invites SET expires_at_utc = now() - interval '1 second' WHERE id = @id", connection))
            {
                markExpired.Parameters.AddWithValue("id", created.Id);
                await markExpired.ExecuteNonQueryAsync();
            }
            Assert.IsNull(await invites.GetRegistrationOptionsAsync(queued.InviteToken, CancellationToken.None));

            var redeemedInvite = await invites.CreateAsync(new CreateStreamerInviteRequest("owner@example.test", "single-use-test", "Single Use", 12), "integration-test", CancellationToken.None);
            var secondMessage = (await outbox.ReadPendingAsync(CancellationToken.None)).Single(item => item.Recipient == "owner@example.test");
            await using (var consume = new NpgsqlCommand("UPDATE streamer_invites SET redeemed_at_utc = now() WHERE id = @id AND redeemed_at_utc IS NULL AND expires_at_utc > now()", connection))
            {
                consume.Parameters.AddWithValue("id", redeemedInvite.Id);
                Assert.AreEqual(1, await consume.ExecuteNonQueryAsync());
                Assert.AreEqual(0, await consume.ExecuteNonQueryAsync());
            }
            Assert.IsNull(await invites.GetRegistrationOptionsAsync(secondMessage.InviteToken, CancellationToken.None));
        }
        finally
        {
            await ExecuteAsync(bootstrap, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    [TestMethod]
    public async Task PasskeyLoginIsDiscoverableAndSignatureCounterNeverDecreases()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("CARD_HUB_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) Assert.Inconclusive("CARD_HUB_TEST_CONNECTION is not configured.");
        var schema = "cardhub_passkey_test_" + Guid.NewGuid().ToString("N");
        await using var bootstrap = NpgsqlDataSource.Create(baseConnectionString);
        await ExecuteAsync(bootstrap, $"CREATE SCHEMA \"{schema}\"");
        var scopedBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schema };
        await using var dataSource = NpgsqlDataSource.Create(scopedBuilder.ConnectionString);
        try
        {
            await new HubDatabaseMigrator(new HubDatabase(dataSource), NullLogger<HubDatabaseMigrator>.Instance).StartAsync(CancellationToken.None);
            var hmacKey = RandomNumberGenerator.GetBytes(32);
            var signingKey = RandomNumberGenerator.GetBytes(32);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CardHub:Identity:HmacKey"] = Convert.ToBase64String(hmacKey),
                ["CardHub:SiteToken:SigningKey"] = Convert.ToBase64String(signingKey),
                ["CardHub:Passkey:RpId"] = "localhost",
                ["CardHub:Passkey:Origins:0"] = "http://localhost:8080"
            }).Build();
            var tenantId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var hasher = new IdentityHasher(config);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using (var tenant = new NpgsqlCommand("INSERT INTO streamers (id, slug, display_name, status) VALUES (@id, 'passkey-test', 'Passkey Test', 'active')", connection))
                {
                    tenant.Parameters.AddWithValue("id", tenantId);
                    await tenant.ExecuteNonQueryAsync();
                }
                await using (var account = new NpgsqlCommand("INSERT INTO streamer_accounts (id, tenant_id, email_hash, passkey_credential_id, passkey_public_key, passkey_user_handle) VALUES (@account, @id, @email, @credential, @key, @handle)", connection))
                {
                    account.Parameters.AddWithValue("id", tenantId);
                    account.Parameters.AddWithValue("account", accountId);
                    account.Parameters.AddWithValue("email", hasher.HashEmail("known@example.test"));
                    account.Parameters.AddWithValue("credential", RandomNumberGenerator.GetBytes(32));
                    account.Parameters.AddWithValue("key", RandomNumberGenerator.GetBytes(65));
                    account.Parameters.AddWithValue("handle", RandomNumberGenerator.GetBytes(32));
                    await account.ExecuteNonQueryAsync();
                }
            }
            var login = new StreamerPasskeyLoginService(new HubDatabase(dataSource),
                new SiteAccessTokenIssuer(config, TimeProvider.System), config, WebApplication.CreateBuilder().Environment, TimeProvider.System);
            var options = await login.CreateOptionsAsync(CancellationToken.None);
            var hasAllowCredentials = options.Options.TryGetProperty("allowCredentials", out var allowCredentials);
            Assert.IsFalse(hasAllowCredentials && allowCredentials.EnumerateArray().Any(), "Discoverable login must not list credential IDs.");

            async Task UpdateCounter(uint count)
            {
                await using var counterConnection = await dataSource.OpenConnectionAsync();
                await using var counterUpdate = new NpgsqlCommand("UPDATE streamer_accounts SET passkey_signature_count = GREATEST(passkey_signature_count, @count) WHERE id = @account", counterConnection);
                counterUpdate.Parameters.AddWithValue("count", (long)count);
                counterUpdate.Parameters.AddWithValue("account", accountId);
                await counterUpdate.ExecuteNonQueryAsync();
            }
            await Task.WhenAll(UpdateCounter(7), UpdateCounter(11), UpdateCounter(4));
            await using var readConnection = await dataSource.OpenConnectionAsync();
            await using var readCounter = new NpgsqlCommand("SELECT passkey_signature_count FROM streamer_accounts WHERE id = @account", readConnection);
            readCounter.Parameters.AddWithValue("account", accountId);
            Assert.AreEqual(11L, (long)(await readCounter.ExecuteScalarAsync() ?? 0L));
        }
        finally
        {
            await ExecuteAsync(bootstrap, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
