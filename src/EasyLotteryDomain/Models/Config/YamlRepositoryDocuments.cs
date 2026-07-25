using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryDomain.Models.Config;

public sealed class SettingsYamlDocument
{
    public int ConfigVersion { get; set; } = 1;
    public LotterySystemSettings SystemSettings { get; set; } = new();
    public LotteryIdSequence IdSequence { get; set; } = new();
    public List<DrawingRulePreset> DrawingRulePresets { get; set; } = [];
    public DrawingRuleSettings DrawingRules { get; set; } = new();
    public VisualStyleSettings VisualStyle { get; set; } = new();
    public ObsLayoutSettings ObsLayout { get; set; } = new();
    public SoundCueSettings SoundCue { get; set; } = new();
    public OvertimeOverlaySettings OvertimeOverlay { get; set; } = new();
    public List<ChangeAuditRecord> AuditRecords { get; set; } = [];
}

public sealed class ActivitiesYamlDocument
{
    public LotteryIdSequence IdSequence { get; set; } = new();
    public List<PokeTemplate> PokeTemplates { get; set; } = [];
    public List<RouletteTemplate> RouletteTemplates { get; set; } = [];
    public List<DonateLotteryActivity> DonateLotteryActivities { get; set; } = [];
}

public sealed class ActivityResultsYamlDocument
{
    public LotteryIdSequence IdSequence { get; set; } = new();
    public List<ActivityResultRecord> ActivityResults { get; set; } = [];
    public List<DonateLotteryDrawRecord> DonateLotteryDrawRecords { get; set; } = [];
    public List<string> ProcessedDonatePaymentIds { get; set; } = [];
}
