using EasyLotteryApplication.Payments;
using EasyLotteryApplication.ObsAssets;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Obs;
using EasyLotteryDomain.Models.Overtime;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryInfrastructure;
using EasyLotteryInfrastructure.DonateActivities;
using EasyLotteryInfrastructure.ObsAssets;
using EasyLotteryInfrastructure.Payments;
using EasyLotteryInfrastructure.Settings;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SqliteProviderRepositoryTests
{
    [TestMethod]
    public async Task PaymentEventRepository_ClaimsAndUpdatesEvents()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await CreateSchemaAsync(options);
            var repository = new SqlitePaymentEventRepository(options);
            var paymentEvent = new ProcessedPaymentEvent
            {
                ProviderId = "test",
                ExternalId = "evt-1",
                Amount = 100
            };

            var claimed = await repository.TryClaimAsync(paymentEvent, TimeSpan.FromMinutes(1));
            var inProgress = await repository.TryClaimAsync(paymentEvent, TimeSpan.FromMinutes(1));
            paymentEvent.ProcessingState = "completed";
            await repository.UpdateAsync(paymentEvent);
            var completed = await repository.TryClaimAsync(paymentEvent, TimeSpan.FromMinutes(1));

            Assert.AreEqual(PaymentEventClaimStatus.Claimed, claimed.Status);
            Assert.AreEqual(PaymentEventClaimStatus.InProgress, inProgress.Status);
            Assert.AreEqual(PaymentEventClaimStatus.AlreadyCompleted, completed.Status);
            Assert.AreEqual(1, (await repository.ListAsync()).Count);
        }
        finally { DeleteDatabase(databasePath); }
    }

    [TestMethod]
    public async Task PaymentOrderAndOvertimeRepositories_RoundTripCollections()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await CreateSchemaAsync(options);
            var orders = new SqlitePaymentOrderRepository(options);
            await orders.SaveAsync([new RegisteredPaymentOrder { ProviderId = "test", MerchantOrderNo = "order-1" }]);
            await orders.MutateAsync(items =>
            {
                items[0].Status = "paid";
                return Task.CompletedTask;
            });

            var feed = new SqliteOvertimeFeedRepository(options);
            await feed.SaveAsync([new OvertimeSupportEvent { DisplayName = "A", Source = OvertimeSupportSource.Manual }]);

            Assert.AreEqual("paid", (await orders.ListAsync()).Single().Status);
            Assert.AreEqual("A", (await feed.ListAsync()).Single().DisplayName);
        }
        finally { DeleteDatabase(databasePath); }
    }

    [TestMethod]
    public async Task ObsAssetRepository_PersistsMetadataAndBlobInSqlite()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await CreateSchemaAsync(options);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var repository = new SqliteObsAssetRepository(options, configuration);
            var saved = await repository.SaveAsync(
                new ObsAsset { FileName = "asset.bin", ContentType = "application/octet-stream" },
                new MemoryStream([1, 2, 3]));

            await using var content = await repository.OpenReadAsync(saved.Id);
            using var bytes = new MemoryStream();
            await content!.CopyToAsync(bytes);

            Assert.AreEqual(1, (await repository.ListAsync()).Count);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, bytes.ToArray());
            Assert.AreEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray())).ToLowerInvariant(), saved.Sha256);
        }
        finally { DeleteDatabase(databasePath); }
    }

    [TestMethod]
    public void SqliteProvider_SelectsSqliteRepositories()
    {
        var databasePath = CreateDatabasePath();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "sqlite",
                ["Storage:ConnectionString"] = $"Data Source={databasePath}"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEasyLotteryInfrastructure();
        using var provider = services.BuildServiceProvider();

        Assert.IsInstanceOfType<SqlitePaymentEventRepository>(provider.GetRequiredService<IPaymentEventRepository>());
        Assert.IsInstanceOfType<SqlitePaymentOrderRepository>(provider.GetRequiredService<IPaymentOrderRepository>());
        Assert.IsInstanceOfType<SqliteOvertimeFeedRepository>(provider.GetRequiredService<IOvertimeFeedRepository>());
        Assert.IsInstanceOfType<SqliteObsAssetRepository>(provider.GetRequiredService<IObsAssetRepository>());
        Assert.IsInstanceOfType<SqliteConfigRepository>(provider.GetRequiredService<IEasyLotteryConfigRepository>());
        Assert.IsInstanceOfType<SqliteSettingsSectionRepository>(provider.GetRequiredService<ISettingsSectionRepository>());
        Assert.IsInstanceOfType<SqliteDonateActivityRepository>(provider.GetRequiredService<IDonateLotteryActivityRepository>());

        DeleteDatabase(databasePath);
    }

    private static IOptions<StorageProviderOptions> CreateOptions(string databasePath) =>
        Options.Create(new StorageProviderOptions
        {
            Provider = "sqlite",
            ConnectionString = $"Data Source={databasePath}"
        });

    private static async Task CreateSchemaAsync(IOptions<StorageProviderOptions> options) =>
        await new SqliteSchemaMigrator(options, NullLogger<SqliteSchemaMigrator>.Instance).StartAsync(CancellationToken.None);

    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"easy-lottery-sqlite-repositories-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
            if (File.Exists(path)) File.Delete(path);
    }
}
