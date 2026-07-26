using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApi;

public interface IEasyLotteryConfigRepository
{
    Task<string> ReadForBrowserAsync(CancellationToken cancellationToken);

    Task SaveBrowserUpdateAsync(string submittedYaml, CancellationToken cancellationToken);

    Task<EasyLotteryConfigDocument> ReadAsync(CancellationToken cancellationToken);

    Task<T> UpdateAsync<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken);
}
