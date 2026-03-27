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
    }
}
