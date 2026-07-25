using EasyLotteryApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SettingsFileStoreTests
{
    [TestMethod]
    public async Task UpdateAsync_SerializesConcurrentDocumentChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-tests-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Directory"] = directory }).Build();
            var store = new SettingsFileStore(configuration, new TestEnvironment(), new ConfigSecretRedactor());

            await Task.WhenAll(
                store.UpdateAsync(document => { document.SystemSettings.ResultNotificationEmail = "results@example.test"; return true; }, CancellationToken.None),
                store.UpdateAsync(document => { document.ProcessedDonatePaymentIds.Add("payment-1"); return true; }, CancellationToken.None));

            var saved = await store.ReadAsync(CancellationToken.None);
            Assert.AreEqual("results@example.test", saved.SystemSettings.ResultNotificationEmail);
            CollectionAssert.Contains(saved.ProcessedDonatePaymentIds, "payment-1");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "EasyLotteryApiTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
