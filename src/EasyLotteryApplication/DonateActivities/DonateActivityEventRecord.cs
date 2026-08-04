using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApplication.DonateActivities;

public sealed class DonateActivityEventRecord
{
    public int EventVersion { get; set; } = 1;
    public string Type { get; set; } = DonateActivityEventTypes.Saved;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int ActivityId { get; set; }
    public DonateLotteryActivity? Activity { get; set; }
}
