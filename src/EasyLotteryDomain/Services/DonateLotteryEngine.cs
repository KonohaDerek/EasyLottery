using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services;

public static class DonateLotteryEngine
{
    public static DonateLotteryProcessResult Process(
        EasyLotteryConfigDocument document,
        string paymentExternalId,
        string donorName,
        decimal amount,
        DateTimeOffset occurredAtUtc,
        Func<double>? nextRandom = null,
        string? donationMessage = null,
        string? paymentMethod = null)
    {
        if (string.IsNullOrWhiteSpace(paymentExternalId))
        {
            return DonateLotteryProcessResult.Ignored("付款識別碼不可為空白。");
        }

        if (document.ProcessedDonatePaymentIds.Any(id => string.Equals(id, paymentExternalId, StringComparison.Ordinal)))
        {
            return DonateLotteryProcessResult.Ignored("此付款已處理過。");
        }

        document.ProcessedDonatePaymentIds.Add(paymentExternalId);
        var results = new List<DonateLotteryDrawRecord>();
        var random = nextRandom ?? Random.Shared.NextDouble;
        foreach (var activity in document.DonateLotteryActivities.Where(activity =>
                     activity.IsEnabled && occurredAtUtc >= activity.StartsAtUtc && occurredAtUtc <= activity.EndsAtUtc))
        {
            var drawCount = activity.MinimumDonationAmount <= 0m ? 0 : (int)(amount / activity.MinimumDonationAmount);
            for (var draw = 0; draw < drawCount; draw++)
            {
                var prize = SelectPrize(activity, random);
                var isWinning = prize is not null;
                if (prize is not null) prize.RemainingQuantity--;
                var record = new DonateLotteryDrawRecord
                {
                    Id = document.IdSequence.NextDonateLotteryDrawRecordId++,
                    ActivityId = activity.Id,
                    PaymentExternalId = paymentExternalId,
                    DonorName = string.IsNullOrWhiteSpace(donorName) ? "匿名贊助者" : donorName.Trim(),
                    Amount = amount,
                    DrawCount = drawCount,
                    PrizeName = prize?.Name ?? "銘謝惠顧",
                    PrizeImageUrl = prize?.ImageUrl ?? "",
                    IsWinning = isWinning,
                    DonationMessage = donationMessage?.Trim() ?? "",
                    PaymentMethod = paymentMethod?.Trim() ?? "",
                    DrawnAtUtc = occurredAtUtc
                };
                document.DonateLotteryDrawRecords.Add(record);
                if (isWinning) results.Add(record);
            }
        }

        return new DonateLotteryProcessResult(true, "", results);
    }
    private static DonateLotteryPrize? SelectPrize(DonateLotteryActivity activity, Func<double> random)
    {
        var prizes = activity.Prizes.Where(item => item.RemainingQuantity > 0).ToList();
        var roll = (decimal)random() * 100m;
        var cursor = 0m;
        foreach (var prize in prizes)
        {
            cursor += Math.Clamp(prize.Probability, 0m, 100m);
            if (roll < cursor) return prize;
        }

        return null;
    }
}

public sealed record DonateLotteryProcessResult(bool Processed, string Reason, IReadOnlyList<DonateLotteryDrawRecord> Wins)
{
    public static DonateLotteryProcessResult Ignored(string reason) => new(false, reason, []);
}
