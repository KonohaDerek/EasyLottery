using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services;

public static class YamlRepositoryMapper
{
    public static (SettingsYamlDocument Settings, ActivitiesYamlDocument Activities, ActivityResultsYamlDocument Results) Split(EasyLotteryConfigDocument source) =>
        (new SettingsYamlDocument { ConfigVersion = source.ConfigVersion, SystemSettings = source.SystemSettings, IdSequence = source.IdSequence, DrawingRulePresets = source.DrawingRulePresets, DrawingRules = source.DrawingRules, VisualStyle = source.VisualStyle, ObsLayout = source.ObsLayout, SoundCue = source.SoundCue, OvertimeOverlay = source.OvertimeOverlay, AuditRecords = source.AuditRecords },
         new ActivitiesYamlDocument { ConfigVersion = source.ConfigVersion, PokeTemplates = source.PokeTemplates, RouletteTemplates = source.RouletteTemplates, DonateLotteryActivities = source.DonateLotteryActivities },
         new ActivityResultsYamlDocument { ConfigVersion = source.ConfigVersion, ActivityResults = source.ActivityResults, DonateLotteryDrawRecords = source.DonateLotteryDrawRecords, ProcessedDonatePaymentIds = source.ProcessedDonatePaymentIds });

    public static EasyLotteryConfigDocument Merge(SettingsYamlDocument settings, ActivitiesYamlDocument activities, ActivityResultsYamlDocument results) => new()
    {
        ConfigVersion = Math.Max(settings.ConfigVersion, Math.Max(activities.ConfigVersion, results.ConfigVersion)),
        SystemSettings = settings.SystemSettings,
        IdSequence = settings.IdSequence,
        DrawingRulePresets = settings.DrawingRulePresets,
        DrawingRules = settings.DrawingRules,
        VisualStyle = settings.VisualStyle,
        ObsLayout = settings.ObsLayout,
        SoundCue = settings.SoundCue,
        OvertimeOverlay = settings.OvertimeOverlay,
        AuditRecords = settings.AuditRecords,
        PokeTemplates = activities.PokeTemplates,
        RouletteTemplates = activities.RouletteTemplates,
        DonateLotteryActivities = activities.DonateLotteryActivities,
        ActivityResults = results.ActivityResults,
        DonateLotteryDrawRecords = results.DonateLotteryDrawRecords,
        ProcessedDonatePaymentIds = results.ProcessedDonatePaymentIds
    };
}
