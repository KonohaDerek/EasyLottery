using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services;

public sealed class DonateLotteryActivityService
{
    private readonly IEasyLotteryConfigStore _configStore;

    public DonateLotteryActivityService(IEasyLotteryConfigStore configStore) => _configStore = configStore;

    public async Task<IReadOnlyList<DonateLotteryActivity>> ListAsync(CancellationToken cancellationToken = default)
    {
        var document = await _configStore.LoadAsync(cancellationToken);
        return document.DonateLotteryActivities.OrderByDescending(activity => activity.Id).ToList();
    }

    public async Task<DonateLotteryActivity> SaveAsync(DonateLotteryActivity activity, CancellationToken cancellationToken = default)
    {
        var document = await _configStore.LoadAsync(cancellationToken);
        activity.Name = activity.Name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(activity.Name)) throw new InvalidOperationException("請輸入活動名稱。");
        if (activity.MinimumDonationAmount <= 0m) throw new InvalidOperationException("最低贊助金額必須大於 0。");
        if (activity.EndsAtUtc <= activity.StartsAtUtc) throw new InvalidOperationException("活動結束時間必須晚於開始時間。");
        activity.Prizes ??= [];
        var probabilityTotal = 0m;
        foreach (var prize in activity.Prizes)
        {
            prize.Id = prize.Id <= 0 ? document.IdSequence.NextDonateLotteryPrizeId++ : prize.Id;
            prize.Name = prize.Name?.Trim() ?? "";
            prize.ImageUrl = prize.ImageUrl?.Trim() ?? "";
            prize.Quantity = Math.Max(0, prize.Quantity);
            prize.RemainingQuantity = Math.Clamp(prize.RemainingQuantity, 0, prize.Quantity);
            prize.Probability = Math.Clamp(prize.Probability, 0m, 100m);
            probabilityTotal += prize.Probability;
        }
        if (probabilityTotal > 100m) throw new InvalidOperationException("獎項機率合計不可超過 100%。");

        if (activity.Id <= 0)
        {
            activity.Id = document.IdSequence.NextDonateLotteryActivityId++;
            document.DonateLotteryActivities.Add(activity);
        }
        else
        {
            var index = document.DonateLotteryActivities.FindIndex(item => item.Id == activity.Id);
            if (index < 0) throw new InvalidOperationException("找不到 Donate 活動。");
            document.DonateLotteryActivities[index] = activity;
        }

        await _configStore.SaveAsync(document, cancellationToken);
        return activity;
    }

    public async Task DeleteAsync(int activityId, CancellationToken cancellationToken = default)
    {
        var document = await _configStore.LoadAsync(cancellationToken);
        var removed = document.DonateLotteryActivities.RemoveAll(activity => activity.Id == activityId);
        if (removed == 0) throw new InvalidOperationException("找不到 Donate 活動。");

        // Keep historical draw records for reports and audit trails. They retain
        // the activity ID even after the editable activity definition is gone.
        await _configStore.SaveAsync(document, cancellationToken);
    }
}
