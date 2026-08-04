using EasyLotteryDomain.Models.Config;

namespace EasyLotteryInfrastructure.Settings;

internal static class YamlDocumentMigrations
{
    public static T Apply<T>(T document, out bool changed)
        where T : class, IYamlVersionedDocument
    {
        changed = false;
        if (document.ConfigVersion > YamlDocumentSchema.CurrentVersion)
        {
            throw new YamlStorageException(
                $"設定版本 {document.ConfigVersion} 高於目前支援版本 {YamlDocumentSchema.CurrentVersion}。",
                typeof(T).Name);
        }

        if (document.ConfigVersion < 2)
        {
            ApplyVersion2Defaults(document);
            document.ConfigVersion = 2;
            changed = true;
        }

        return document;
    }

    private static void ApplyVersion2Defaults(IYamlVersionedDocument document)
    {
        if (document is ActivitiesYamlDocument activities)
        {
            activities.PokeTemplates ??= [];
            activities.RouletteTemplates ??= [];
            activities.DonateLotteryActivities ??= [];
            foreach (var activity in activities.DonateLotteryActivities)
            {
                activity.PublicId = activity.PublicId == Guid.Empty ? Guid.NewGuid() : activity.PublicId;
                activity.PolaroidTemplateKey = string.IsNullOrWhiteSpace(activity.PolaroidTemplateKey)
                    ? "classic"
                    : activity.PolaroidTemplateKey.Trim().ToLowerInvariant();
                activity.ResultDisplayDurationSeconds = activity.ResultDisplayDurationSeconds <= 0 ? 15 : activity.ResultDisplayDurationSeconds;
                activity.AnimationDurationSeconds = activity.AnimationDurationSeconds <= 0 ? 8 : activity.AnimationDurationSeconds;
                activity.Prizes ??= [];
            }
        }
        else if (document is ActivityResultsYamlDocument results)
        {
            results.ActivityResults ??= [];
            results.DonateLotteryDrawRecords ??= [];
            results.ProcessedDonatePaymentIds ??= [];
        }
        else if (document is SettingsYamlDocument settings)
        {
            settings.SystemSettings ??= new LotterySystemSettings();
            settings.IdSequence ??= new LotteryIdSequence();
            settings.DrawingRulePresets ??= [];
            settings.DrawingRules ??= new DrawingRuleSettings();
            settings.VisualStyle ??= new VisualStyleSettings();
            settings.ObsLayout ??= new ObsLayoutSettings();
            settings.SoundCue ??= new SoundCueSettings();
            settings.OvertimeOverlay ??= new OvertimeOverlaySettings();
            settings.AuditRecords ??= [];
        }
    }
}
