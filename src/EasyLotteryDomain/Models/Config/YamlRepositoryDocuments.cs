using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryDomain.Models.Config;

public static class YamlDocumentSchema
{
    public const int CurrentVersion = 2;
}

public interface IYamlVersionedDocument
{
    int ConfigVersion { get; set; }
}

public sealed class SettingsYamlDocument : IYamlVersionedDocument
{
    public int ConfigVersion { get; set; } = YamlDocumentSchema.CurrentVersion;
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

public sealed class ActivitiesYamlDocument : IYamlVersionedDocument
{
    public int ConfigVersion { get; set; } = YamlDocumentSchema.CurrentVersion;
    public List<PokeTemplate> PokeTemplates { get; set; } = [];
    public List<RouletteTemplate> RouletteTemplates { get; set; } = [];
    public List<DonateLotteryActivity> DonateLotteryActivities { get; set; } = [];
}

public sealed class ActivityResultsYamlDocument : IYamlVersionedDocument
{
    public int ConfigVersion { get; set; } = YamlDocumentSchema.CurrentVersion;
    public List<ActivityResultRecord> ActivityResults { get; set; } = [];
    public List<DonateLotteryDrawRecord> DonateLotteryDrawRecords { get; set; } = [];
    public List<string> ProcessedDonatePaymentIds { get; set; } = [];
}
