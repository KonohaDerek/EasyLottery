using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApplication.DonateActivities;

public static class DonateActivityRules
{
    public static DonateLotteryActivity NormalizeForSave(DonateLotteryActivity activity, IReadOnlyCollection<DonateLotteryActivity> existingActivities)
    {
        activity.Name = activity.Name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(activity.Name))
        {
            throw new InvalidOperationException("請輸入活動名稱。");
        }

        if (activity.MinimumDonationAmount <= 0m)
        {
            throw new InvalidOperationException("最低贊助金額必須大於 0。");
        }

        if (activity.EndsAtUtc <= activity.StartsAtUtc)
        {
            throw new InvalidOperationException("活動結束時間必須晚於開始時間。");
        }

        if (activity.EndsAtUtc < DateTimeOffset.UtcNow.AddMinutes(30))
        {
            throw new InvalidOperationException("活動結束時間至少必須在目前時間 30 分鐘後。");
        }

        if (activity.ResultDisplayDurationSeconds is < 3 or > 300)
        {
            throw new InvalidOperationException("結果顯示秒數必須介於 3 至 300 秒。");
        }

        if (activity.AnimationDurationSeconds is < 3 or > 30)
        {
            throw new InvalidOperationException("抽獎動畫秒數必須介於 3 至 30 秒。");
        }

        var normalized = Clone(activity);
        normalized.WebmAnimationUrl = normalized.WebmAnimationUrl?.Trim() ?? "";
        normalized.WebmPosterUrl = normalized.WebmPosterUrl?.Trim() ?? "";
        normalized.PolaroidTemplateKey = string.IsNullOrWhiteSpace(normalized.PolaroidTemplateKey)
            ? "classic"
            : normalized.PolaroidTemplateKey.Trim().ToLowerInvariant();
        var maxActivityId = existingActivities.Select(item => item.Id).DefaultIfEmpty(0).Max();
        var maxPrizeId = existingActivities.SelectMany(item => item.Prizes ?? []).Select(item => item.Id).DefaultIfEmpty(0).Max();

        if (normalized.PublicId == Guid.Empty || existingActivities.Any(item => item.Id != normalized.Id && item.PublicId == normalized.PublicId))
        {
            normalized.PublicId = Guid.NewGuid();
        }

        if (normalized.Id <= 0)
        {
            normalized.Id = maxActivityId + 1;
        }
        else if (!existingActivities.Any(item => item.Id == normalized.Id))
        {
            throw new InvalidOperationException("找不到 Donate 活動。");
        }

        normalized.Prizes ??= [];
        var nextPrizeId = maxPrizeId + 1;
        var probabilityTotal = 0m;
        foreach (var prize in normalized.Prizes)
        {
            prize.Id = prize.Id <= 0 ? nextPrizeId++ : prize.Id;
            prize.Name = prize.Name?.Trim() ?? "";
            prize.ImageUrl = prize.ImageUrl?.Trim() ?? "";
            prize.Quantity = Math.Max(0, prize.Quantity);
            prize.RemainingQuantity = Math.Clamp(prize.RemainingQuantity, 0, prize.Quantity);
            prize.Probability = Math.Clamp(prize.Probability, 0m, 100m);
            probabilityTotal += prize.Probability;
        }

        if (probabilityTotal > 100m)
        {
            throw new InvalidOperationException("獎項機率合計不可超過 100%。");
        }

        return normalized;
    }

    private static DonateLotteryActivity Clone(DonateLotteryActivity source) =>
        new()
        {
            Id = source.Id,
            PublicId = source.PublicId,
            Name = source.Name,
            Type = source.Type,
            MinimumDonationAmount = source.MinimumDonationAmount,
            StartsAtUtc = source.StartsAtUtc,
            EndsAtUtc = source.EndsAtUtc,
            Animation = source.Animation,
            PolaroidTemplateKey = source.PolaroidTemplateKey,
            UseAiCongratulation = source.UseAiCongratulation,
            ShowDonateInformation = source.ShowDonateInformation,
            ResultDisplayDurationSeconds = source.ResultDisplayDurationSeconds,
            AnimationDurationSeconds = source.AnimationDurationSeconds,
            UseWebmAnimation = source.UseWebmAnimation,
            WebmAnimationUrl = source.WebmAnimationUrl,
            WebmPosterUrl = source.WebmPosterUrl,
            WebmAnimationLoop = source.WebmAnimationLoop,
            IsEnabled = source.IsEnabled,
            Prizes = source.Prizes?.Select(prize => new DonateLotteryPrize
            {
                Id = prize.Id,
                Name = prize.Name,
                ImageUrl = prize.ImageUrl,
                Quantity = prize.Quantity,
                RemainingQuantity = prize.RemainingQuantity,
                Probability = prize.Probability,
                IsGrandPrize = prize.IsGrandPrize
            }).ToList() ?? []
        };
}
