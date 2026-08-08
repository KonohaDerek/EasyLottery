using EasyLotteryInfrastructure.Storage;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class StorageProviderOptionsTests
{
    [TestMethod]
    public void Validate_accepts_supported_providers()
    {
        foreach (var provider in new[] { "yaml", "sqlite", "postgresql" })
        {
            var options = new StorageProviderOptions { Provider = provider, ConnectionString = provider == "sqlite" ? "Data Source=test.db" : null };
            options.Validate();
        }
    }

    [TestMethod]
    public void Validate_rejects_sqlite_without_connection_string()
    {
        var options = new StorageProviderOptions { Provider = "sqlite" };
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [TestMethod]
    public void ValidateAdapterAvailability_rejects_unregistered_postgresql_adapter()
    {
        var options = new StorageProviderOptions { Provider = "postgresql" };

        Assert.Throws<InvalidOperationException>(() => options.ValidateAdapterAvailability());
    }
}
