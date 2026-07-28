namespace EasyLotteryDomain.Models.Config;

public enum DonateLotteryActivityType
{
    Postcard,
    Polaroid
}

public enum DonateLotteryAnimation
{
    IchibanKuji,
    Gacha,
    Garagara,
    MoneyBox
}

public sealed class DonateLotteryActivity
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DonateLotteryActivityType Type { get; set; }
    public decimal MinimumDonationAmount { get; set; } = 100m;
    public DateTimeOffset StartsAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAtUtc { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
    public DonateLotteryAnimation Animation { get; set; } = DonateLotteryAnimation.IchibanKuji;
    /// <summary>拍立得活動使用的結果模板；舊活動會回退為 classic。</summary>
    public string PolaroidTemplateKey { get; set; } = "classic";
    public bool UseAiCongratulation { get; set; }
    /// <summary>Donate 抽獎結果在 OBS 顯示的秒數。</summary>
    public int ResultDisplayDurationSeconds { get; set; } = 15;
    /// <summary>Donate 抽獎動畫在揭曉前播放的秒數。</summary>
    public int AnimationDurationSeconds { get; set; } = 8;
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
    /// <summary>此獎項在單次抽獎中所佔百分比（0 至 100）。未配置的餘額為銘謝惠顧。</summary>
    public decimal Probability { get; set; }
    public bool IsGrandPrize { get; set; }
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
    public bool IsWinning { get; set; }
    public string DonationMessage { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public DateTimeOffset DrawnAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
