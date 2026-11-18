using System.Net;
using EasyLotteryApi.Shop;
using EasyLotteryInfrastructure.Shop;
using EasyLotteryInfrastructure.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class HubLinkSettingsServiceTests
{
    [TestMethod]
    public async Task StoredCredential_IsReusedOnlyForItsSavedHubAndClientId()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-hub-link-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(directory, "shop.db");
        var keyDirectory = Path.Combine(directory, "keys");
        Directory.CreateDirectory(keyDirectory);
        var options = Options.Create(new StorageProviderOptions
        {
            Provider = "sqlite",
            ConnectionString = $"Data Source={databasePath}"
        });
        var handler = new CountingHandler();
        using var httpClient = new HttpClient(handler);
        using var hubClient = new CardHubClient(httpClient);
        using var services = new ServiceCollection()
            .AddDataProtection()
            .SetApplicationName("EasyLottery.HubLinkSettingsServiceTests")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
            .Services
            .BuildServiceProvider();
        var repository = new SqliteHubLinkRepository(options.Value);
        var service = new HubLinkSettingsService(repository, services.GetRequiredService<IDataProtectionProvider>(), hubClient);

        try
        {
            await new SqliteSchemaMigrator(options, NullLogger<SqliteSchemaMigrator>.Instance)
                .StartAsync(CancellationToken.None);
            var original = new HubLinkWriteRequest
            {
                HubBaseUrl = "https://hub.example.test",
                SiteClientId = "site-1",
                ClientSecret = "secret-value"
            };
            Assert.AreEqual(HubLinkStatuses.Linked, (await service.SaveAsync(original, CancellationToken.None)).Status);
            Assert.AreEqual(1, handler.CallCount);

            var changedBaseUrl = new HubLinkWriteRequest
            {
                HubBaseUrl = "https://attacker.example.test",
                SiteClientId = "site-1"
            };
            var testResult = await service.TestAsync(changedBaseUrl, CancellationToken.None);
            Assert.AreEqual(HubLinkStatuses.NotLinked, testResult.Status);
            var changedBaseRejected = false;
            try
            {
                await service.SaveAsync(changedBaseUrl, CancellationToken.None);
            }
            catch (ArgumentException)
            {
                changedBaseRejected = true;
            }
            Assert.IsTrue(changedBaseRejected, "Changing the Hub URL without entering a new secret must be rejected.");
            Assert.AreEqual(1, handler.CallCount, "The saved secret must not be sent to a changed Hub URL.");

            var sameHubWithoutSecret = new HubLinkWriteRequest
            {
                HubBaseUrl = original.HubBaseUrl,
                SiteClientId = original.SiteClientId
            };
            Assert.AreEqual(HubLinkStatuses.Linked, (await service.SaveAsync(sameHubWithoutSecret, CancellationToken.None)).Status);
            Assert.AreEqual(2, handler.CallCount);

            await using var connection = new SqliteConnection(options.Value.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT encrypted_secret FROM hub_link WHERE id = 1;";
            var stored = (string)(await command.ExecuteScalarAsync())!;
            Assert.IsFalse(stored.Contains("secret-value", StringComparison.Ordinal));
        }
        finally
        {
            foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
                if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"test-token\"}")
            });
        }
    }
}
