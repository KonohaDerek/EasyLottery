using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryInfrastructure.DonateActivities;
using EasyLotteryInfrastructure.Settings;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SqliteStorageRoundTripTests
{
    [TestMethod]
    public async Task ConfigStore_RoundTripsDomainDocument()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await CreateSchemaAsync(options);
            var store = new SqliteConfigStore(options);
            var document = new EasyLotteryConfigDocument
            {
                SystemSettings = { ResultNotificationEmail = "sqlite@example.test" },
                PokeTemplates = [new PokeTemplate { Id = 11, PublicId = Guid.NewGuid(), Name = "SQLite 戳戳樂" }],
                RouletteTemplates = [new RouletteTemplate { Id = 12, PublicId = Guid.NewGuid(), Name = "SQLite 轉盤" }],
                ActivityResults = [new ActivityResultRecord { Id = 13, ActivityName = "SQLite 結果" }]
            };

            await store.SaveAsync(document);
            var restored = await store.LoadAsync();

            Assert.AreEqual("sqlite@example.test", restored.SystemSettings.ResultNotificationEmail);
            Assert.AreEqual("SQLite 戳戳樂", restored.PokeTemplates.Single().Name);
            Assert.AreEqual("SQLite 轉盤", restored.RouletteTemplates.Single().Name);
            Assert.AreEqual(13, restored.ActivityResults.Single().Id);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [TestMethod]
    public async Task DonateRepository_RoundTripsActivitySnapshot()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await CreateSchemaAsync(options);
            var repository = new SqliteDonateActivityRepository(options);
            var activity = new DonateLotteryActivity
            {
                Id = 21,
                Name = "SQLite Donate",
                Prizes = [new DonateLotteryPrize { Id = 1, Name = "獎品", RemainingQuantity = 3 }]
            };

            await repository.SaveSnapshotAsync([activity]);
            var restored = await repository.GetByIdAsync(21);

            Assert.IsNotNull(restored);
            Assert.AreEqual("SQLite Donate", restored.Name);
            Assert.AreEqual(3, restored.Prizes.Single().RemainingQuantity);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [TestMethod]
    public async Task SchemaMigration_RecordsCurrentVersion()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await CreateSchemaAsync(options);
            await using var connection = new SqliteConnection(options.Value.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(version) FROM schema_migrations";

            Assert.AreEqual(2L, (long)(await command.ExecuteScalarAsync())!);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static IOptions<StorageProviderOptions> CreateOptions(string databasePath) =>
        Options.Create(new StorageProviderOptions
        {
            Provider = "sqlite",
            ConnectionString = $"Data Source={databasePath}"
        });

    private static async Task CreateSchemaAsync(IOptions<StorageProviderOptions> options) =>
        await new SqliteSchemaMigrator(options, NullLogger<SqliteSchemaMigrator>.Instance)
            .StartAsync(CancellationToken.None);

    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"easy-lottery-sqlite-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
            if (File.Exists(path)) File.Delete(path);
    }
}
