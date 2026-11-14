using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryInfrastructure.Storage;

public static class CardShopSqliteConnection
{
    public static string ResolveConnectionString(IConfiguration configuration, IHostEnvironment environment, StorageProviderOptions storage)
    {
        if (storage.NormalizedProvider == "sqlite") return storage.ConnectionString!;

        var databaseDirectory = configuration["Storage:Directory"]
            ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(databaseDirectory);
        var configuredPath = configuration["Shop:SiteDatabasePath"];
        var databasePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(databaseDirectory, "card-hub.sqlite")
            : Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(databaseDirectory, configuredPath);

        return new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }
}
