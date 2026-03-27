namespace EasyLotteryDomain.Models.Overtime
{
    public enum OvertimeSupportSource
    {
        SuperChat = 1,
        EcpayDonate = 2,
        Manual = 3
    }

    public sealed class OvertimeSupportEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public OvertimeSupportSource Source { get; set; }

        public string SourceLabel { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string Message { get; set; } = "";

        public decimal? Amount { get; set; }

        public string AmountDisplay { get; set; } = "";

        public string Currency { get; set; } = "TWD";

        public string AvatarUrl { get; set; } = "";

        public string Color { get; set; } = "#ff85b4";

        public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public string? ExternalId { get; set; }
    }
}
