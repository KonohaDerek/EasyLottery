namespace EasyLotteryInfrastructure.Storage;

public interface IStorageGateProvider
{
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default, params string[] paths);
}
