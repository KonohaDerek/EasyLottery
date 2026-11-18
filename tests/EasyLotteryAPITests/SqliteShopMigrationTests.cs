using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SqliteShopMigrationTests
{
    [TestMethod]
    public async Task Migrator_AddsAllD3SiteTablesAndLeavesProductTypeOpen()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"easy-lottery-shop-schema-{Guid.NewGuid():N}.db");
        var options = Options.Create(new StorageProviderOptions
        {
            Provider = "sqlite",
            ConnectionString = $"Data Source={databasePath}"
        });

        try
        {
            await new SqliteSchemaMigrator(options, NullLogger<SqliteSchemaMigrator>.Instance)
                .StartAsync(CancellationToken.None);

            await using var connection = new SqliteConnection(options.Value.ConnectionString);
            await connection.OpenAsync();
            await using var version = connection.CreateCommand();
            version.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = 3;";
            Assert.AreEqual(1L, (long)(await version.ExecuteScalarAsync())!);

            await using var tables = connection.CreateCommand();
            tables.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            var tableNames = new HashSet<string>(StringComparer.Ordinal);
            await using (var reader = await tables.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) tableNames.Add(reader.GetString(0));
            }
            foreach (var name in new[] { "shop_products", "card_rarities", "card_sets", "cards", "card_packs", "hub_link", "hub_publish_log" })
                Assert.IsTrue(tableNames.Contains(name), $"Missing D3 table: {name}");

            await using var productType = connection.CreateCommand();
            productType.CommandText = "INSERT INTO shop_products (id, type, status, title) VALUES ('future', 'future_product_type', 'draft', 'Future');";
            await productType.ExecuteNonQueryAsync();
        }
        finally
        {
            foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
                if (File.Exists(path)) File.Delete(path);
        }
    }
}
