namespace EasyLotteryDomain.Models.Config
{
    public sealed class OvertimeRewardRule
    {
        public bool IsEnabled { get; set; } = true;

        public decimal AmountThreshold { get; set; }

        public int AddHours { get; set; }

        public int AddMinutes { get; set; }

        public string Label { get; set; } = "";
    }
}
