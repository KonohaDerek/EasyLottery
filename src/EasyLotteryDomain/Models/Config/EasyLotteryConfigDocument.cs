using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryDomain.Models.Config
{
    public sealed class EasyLotteryConfigDocument
    {
        public int ConfigVersion { get; set; } = 1;

        public LotterySystemSettings SystemSettings { get; set; } = new();

        public LotteryIdSequence IdSequence { get; set; } = new();

        public List<PokeTemplate> PokeTemplates { get; set; } = new();

        public List<RouletteTemplate> RouletteTemplates { get; set; } = new();

        public List<ActivityResultRecord> ActivityResults { get; set; } = new();

        public List<DrawingRulePreset> DrawingRulePresets { get; set; } = new();

        public DrawingRuleSettings DrawingRules { get; set; } = new();
        public VisualStyleSettings VisualStyle { get; set; } = new();
        public OvertimeOverlaySettings OvertimeOverlay { get; set; } = new();
    }

    public sealed class LotterySystemSettings
    {
        public YouTubeApiSettings YouTube { get; set; } = new();

        public string OpenAIKey { get; set; } = "";
    }

    public sealed class YouTubeApiSettings
    {
        public string ApiKey { get; set; } = "";

        public string CredentialsBase64 { get; set; } = "";

        public string RedirectUri { get; set; } = "";

        public string RefreshToken { get; set; } = "";
    }

    public sealed class LotteryIdSequence
    {
        public int NextPokeTemplateId { get; set; } = 1;

        public int NextPokeCellId { get; set; } = 1;

        public int NextRouletteTemplateId { get; set; } = 1;

        public int NextRouletteSegmentId { get; set; } = 1;

        public int NextActivityResultId { get; set; } = 1;
    }

    public sealed class DrawingRuleSettings
    {
        public Dictionary<string, int> LevelRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
