using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services
{
    public interface IEasyLotteryConfigStore
    {
        Task<EasyLotteryConfigDocument> LoadAsync(CancellationToken cancellationToken = default);

        Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default);
    }
}