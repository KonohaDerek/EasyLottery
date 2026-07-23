namespace EasyLotteryDomain.Models.Config
{
    public sealed class VisualStyleSettings
    {
        public string ActiveThemeKey { get; set; } = "dashboard-console";

        /// <summary>Optional HTTPS image displayed behind the application shell.</summary>
        public string BackgroundImageUrl { get; set; } = "";

        /// <summary>Optional HTTPS image displayed in the application banner.</summary>
        public string BannerImageUrl { get; set; } = "";
    }
}
