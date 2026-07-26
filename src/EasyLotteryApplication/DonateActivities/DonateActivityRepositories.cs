using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApplication.DonateActivities;

public interface IDonateLotteryActivityRepository
{
    Task<IReadOnlyList<DonateLotteryActivity>> ListAsync(CancellationToken cancellationToken = default);

    Task<DonateLotteryActivity?> GetByIdAsync(int activityId, CancellationToken cancellationToken = default);

    Task SaveSnapshotAsync(IReadOnlyList<DonateLotteryActivity> activities, CancellationToken cancellationToken = default);
}

public interface IDonateActivityEventStore
{
    Task AppendAsync(DonateActivityEventRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DonateActivityEventRecord>> ReadAllAsync(CancellationToken cancellationToken = default);
}
