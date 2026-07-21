namespace EasyLotteryDomain.Models.Config
{
    public sealed class OvertimeOverlaySettings
    {
        public bool IsEnabled { get; set; } = false;

        public string Title { get; set; } = "加班台";

        public string Subtitle { get; set; } = "SuperChat / 綠界贊助";

        public string ThemeKey { get; set; } = "festival-stage";

        public int MaxVisibleItems { get; set; } = 8;

        public bool ShowAmounts { get; set; } = true;

        public bool ShowAvatars { get; set; } = true;

        public decimal DemoSuperChatAmount { get; set; } = 1000m;

        public decimal DemoEcpayAmount { get; set; } = 2000m;

        public string MessageTemplateKey { get; set; } = "default";

        public string TextAnimationKey { get; set; } = "slide-in";

        public DateTimeOffset? StreamStartedAtUtc { get; set; }

        public DateTimeOffset? PlannedEndAtUtc { get; set; }

        public string SessionState { get; set; } = OvertimeSessionStates.Idle;

        public DateTimeOffset? PausedAtUtc { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public bool EnableCompletionFireworks { get; set; } = true;

        public string CompletionFireworksStyle { get; set; } = "classic";

        public int CompletionFireworksDurationSeconds { get; set; } = 6;

        public string CompletionFireworksGifUrl { get; set; } = "";

        public int SupportMessageVisibleSeconds { get; set; } = 8;

        public List<OvertimeRewardRule> RewardRules { get; set; } = new()
        {
            new OvertimeRewardRule
            {
                Label = "入門加班",
                AmountThreshold = 100,
                AddMinutes = 10
            },
            new OvertimeRewardRule
            {
                Label = "熱心支持",
                AmountThreshold = 300,
                AddMinutes = 30
            },
            new OvertimeRewardRule
            {
                Label = "長時加班",
                AmountThreshold = 1000,
                AddHours = 2
            }
        };
    }

    public static class OvertimeSessionStates
    {
        public const string Idle = "idle";
        public const string Running = "running";
        public const string Paused = "paused";
        public const string Completed = "completed";
        public const string Ended = "ended";

        public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            Running => Running,
            Paused => Paused,
            Completed => Completed,
            Ended => Ended,
            _ => Idle
        };
    }
}
