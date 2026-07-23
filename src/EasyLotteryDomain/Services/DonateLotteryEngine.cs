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
        Func<double>? nextRandom = null)
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
                if (random() >= (double)Math.Clamp(activity.WinProbability, 0m, 1m))
                {
                    continue;
                }

                var available = activity.Prizes.Where(prize => prize.RemainingQuantity > 0).ToList();
                if (available.Count == 0)
                {
                    break;
                }

                var prize = available[Math.Min((int)(random() * available.Count), available.Count - 1)];
                prize.RemainingQuantity--;
                var record = new DonateLotteryDrawRecord
                {
                    Id = document.IdSequence.NextDonateLotteryDrawRecordId++,
                    ActivityId = activity.Id,
                    PaymentExternalId = paymentExternalId,
                    DonorName = string.IsNullOrWhiteSpace(donorName) ? "匿名贊助者" : donorName.Trim(),
                    Amount = amount,
                    DrawCount = drawCount,
                    PrizeName = prize.Name,
                    PrizeImageUrl = prize.ImageUrl,
                    DrawnAtUtc = occurredAtUtc
                };
                document.DonateLotteryDrawRecords.Add(record);
                results.Add(record);
            }
        }

        return new DonateLotteryProcessResult(true, "", results);
    }
}

public sealed record DonateLotteryProcessResult(bool Processed, string Reason, IReadOnlyList<DonateLotteryDrawRecord> Wins)
{
    public static DonateLotteryProcessResult Ignored(string reason) => new(false, reason, []);
}
