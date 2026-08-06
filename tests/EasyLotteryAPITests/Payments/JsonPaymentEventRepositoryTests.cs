using EasyLotteryApplication.Payments;
using EasyLotteryInfrastructure.Payments;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryApiTests.Payments;

[TestClass]
public sealed class JsonPaymentEventRepositoryTests
{
    [TestMethod]
    public async Task TryClaimAsync_ConcurrentCallbacks_OnlyOneClaimIsGranted()
    {
        using var storage = new TemporaryStorage();
        var repository = CreateRepository(storage.Path);
        var callbacks = Enumerable.Range(0, 2)
            .Select(_ => repository.TryClaimAsync(CreateEvent(), TimeSpan.FromMinutes(5)))
            .ToArray();

        var claims = await Task.WhenAll(callbacks);

        Assert.AreEqual(1, claims.Count(result => result.Status == PaymentEventClaimStatus.Claimed));
        Assert.AreEqual(1, claims.Count(result => result.Status == PaymentEventClaimStatus.InProgress));
        Assert.AreEqual(1, (await repository.ListAsync()).Count);
    }

    [TestMethod]
    public async Task TryClaimAsync_FailedClaim_CanBeRecoveredWithoutAppendingAnotherEvent()
    {
        using var storage = new TemporaryStorage();
        var repository = CreateRepository(storage.Path);
        var first = await repository.TryClaimAsync(CreateEvent(), TimeSpan.FromMinutes(5));
        first.Event.ProcessingState = "failed";
        first.Event.ProcessingFailureReason = "temporary failure";
        await repository.UpdateAsync(first.Event);

        var retry = await repository.TryClaimAsync(CreateEvent(), TimeSpan.FromMinutes(5));

        Assert.AreEqual(PaymentEventClaimStatus.Claimed, retry.Status);
        Assert.AreEqual(2, retry.Event.ProcessingAttempt);
        Assert.AreEqual(1, (await repository.ListAsync()).Count);
    }

    [TestMethod]
    public async Task TryClaimAsync_CompletedLegacyEvent_IsReturnedAsDuplicate()
    {
        using var storage = new TemporaryStorage();
        var repository = CreateRepository(storage.Path);
        await repository.AppendAsync(CreateEvent());

        var duplicate = await repository.TryClaimAsync(CreateEvent(), TimeSpan.FromMinutes(5));

        Assert.AreEqual(PaymentEventClaimStatus.AlreadyCompleted, duplicate.Status);
    }

    [TestMethod]
    public async Task TryClaimAsync_ExpiredProcessingLease_CanBeRecovered()
    {
        using var storage = new TemporaryStorage();
        var repository = CreateRepository(storage.Path);
        var abandoned = CreateEvent();
        abandoned.ProcessingState = "processing";
        abandoned.ProcessingClaimedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        abandoned.ProcessingAttempt = 1;
        await repository.AppendAsync(abandoned);

        var retry = await repository.TryClaimAsync(CreateEvent(), TimeSpan.FromMinutes(5));

        Assert.AreEqual(PaymentEventClaimStatus.Claimed, retry.Status);
        Assert.AreEqual(2, retry.Event.ProcessingAttempt);
    }

    private static JsonPaymentEventRepository CreateRepository(string path)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Directory"] = path })
            .Build();
        return new JsonPaymentEventRepository(configuration, new TestHostEnvironment(path), new StorageGateProvider());
    }

    private static ProcessedPaymentEvent CreateEvent() => new()
    {
        ProviderId = "test-provider",
        ExternalId = "external-123",
        MerchantOrderNo = "order-123",
        Amount = 100m,
        PaidAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class TemporaryStorage : IDisposable
    {
        public TemporaryStorage()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"easy-lottery-payment-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "EasyLotteryTests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
