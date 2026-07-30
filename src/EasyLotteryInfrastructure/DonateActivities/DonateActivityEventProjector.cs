using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryInfrastructure.DonateActivities;

internal static class DonateActivityEventProjector
{
    public static void Apply(List<DonateLotteryActivity> activities, DonateActivityEventRecord record)
    {
        switch (record.Type)
        {
            case DonateActivityEventTypes.Saved:
                if (record.Activity is null)
                {
                    return;
                }

                var index = activities
                    .Select((activity, position) => (activity, position))
                    .FirstOrDefault(item => item.activity.Id == record.Activity.Id);

                if (index.activity is null)
                {
                    activities.Add(Clone(record.Activity));
                }
                else
                {
                    activities.RemoveAt(index.position);
                    activities.Add(Clone(record.Activity));
                }

                break;

            case DonateActivityEventTypes.Deleted:
                var deleteIndex = activities
                    .Select((activity, position) => (activity, position))
                    .FirstOrDefault(item => item.activity.Id == record.ActivityId);

                if (deleteIndex.activity is not null)
                {
                    activities.RemoveAt(deleteIndex.position);
                }

                break;
        }
    }

    public static List<DonateLotteryActivity> Rebuild(IEnumerable<DonateActivityEventRecord> events)
    {
        var activities = new List<DonateLotteryActivity>();
        foreach (var record in events.OrderBy(eventRecord => eventRecord.OccurredAtUtc))
        {
            Apply(activities, record);
        }

        return activities.OrderByDescending(activity => activity.Id).ToList();
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
            IsEnabled = source.IsEnabled,
            Prizes = source.Prizes.Select(prize => new DonateLotteryPrize
            {
                Id = prize.Id,
                Name = prize.Name,
                ImageUrl = prize.ImageUrl,
                Quantity = prize.Quantity,
                RemainingQuantity = prize.RemainingQuantity,
                Probability = prize.Probability,
                IsGrandPrize = prize.IsGrandPrize
            }).ToList()
        };
}
