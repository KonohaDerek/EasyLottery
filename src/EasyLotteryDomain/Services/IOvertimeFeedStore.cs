using System.Threading.Tasks;
using EasyLotteryDomain.Models.Overtime;

namespace EasyLotteryDomain.Services
{
    public interface IOvertimeFeedStore
    {
        Task<IReadOnlyList<OvertimeSupportEvent>> SnapshotAsync();

        Task<OvertimeSupportEvent> AddAsync(OvertimeSupportEvent entry);

        Task ClearAsync();
    }
}
