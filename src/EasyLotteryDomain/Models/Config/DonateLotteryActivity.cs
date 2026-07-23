namespace EasyLotteryDomain.Models.Config;

public enum DonateLotteryActivityType
{
    Postcard,
    Polaroid
}

public sealed class DonateLotteryActivity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DonateLotteryActivityType Type { get; set; }
    public decimal MinimumDonationAmount { get; set; } = 100m;
    public DateTimeOffset StartsAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAtUtc { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
    public decimal WinProbability { get; set; } = 1m;
    public bool IsEnabled { get; set; }
    public List<DonateLotteryPrize> Prizes { get; set; } = [];
}

public sealed class DonateLotteryPrize
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public int RemainingQuantity { get; set; } = 1;
}

public sealed class DonateLotteryDrawRecord
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public string PaymentExternalId { get; set; } = "";
    public string DonorName { get; set; } = "匿名贊助者";
    public decimal Amount { get; set; }
    public int DrawCount { get; set; }
    public string PrizeName { get; set; } = "";
    public string PrizeImageUrl { get; set; } = "";
    public DateTimeOffset DrawnAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
