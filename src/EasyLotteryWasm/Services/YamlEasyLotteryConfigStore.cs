using System;
using System.Collections.Generic;
using System.Text.Json;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;
using EasyLotteryWasm.Models;
using Microsoft.JSInterop;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EasyLotteryWasm.Services
{
    public sealed class YamlEasyLotteryConfigStore : IEasyLotteryConfigStore
    {
        private static readonly JsonSerializerOptions CloneSerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly IConfiguration _configuration;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<YamlEasyLotteryConfigStore> _logger;
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private EasyLotteryConfigDocument? _cachedDocument;

        public YamlEasyLotteryConfigStore(
            IConfiguration configuration,
            IJSRuntime jsRuntime,
            ILogger<YamlEasyLotteryConfigStore> logger)
        {
            _configuration = configuration;
            _jsRuntime = jsRuntime;
            _logger = logger;
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();
        }

        public async Task<EasyLotteryConfigDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                _cachedDocument = await LoadFromJsAsync(cancellationToken);
                return CloneDocument(_cachedDocument);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default)
        {
            var normalized = Normalize(CloneDocument(document));

            await _gate.WaitAsync(cancellationToken);
            try
            {
                _cachedDocument = CloneDocument(normalized);
                var yaml = _serializer.Serialize(normalized);
                await _jsRuntime.InvokeVoidAsync("easyLotteryConfig.write", cancellationToken, yaml);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Failed to write YAML configuration to the browser bridge. Using in-memory cache only.");
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<EasyLotteryConfigDocument> LoadFromJsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var yaml = await _jsRuntime.InvokeAsync<string>("easyLotteryConfig.read", cancellationToken);
                if (string.IsNullOrWhiteSpace(yaml))
                {
                    return CreateDefaultDocument();
                }

                try
                {
                    var document = _deserializer.Deserialize<EasyLotteryConfigDocument>(yaml) ?? new EasyLotteryConfigDocument();
                    return Normalize(document);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize YAML configuration. Falling back to defaults.");
                    return CreateDefaultDocument();
                }
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Failed to read YAML configuration from the browser bridge. Falling back to defaults.");
                return CreateDefaultDocument();
            }
        }

        private EasyLotteryConfigDocument CreateDefaultDocument()
        {
            var document = new EasyLotteryConfigDocument
            {
                SystemSettings = new LotterySystemSettings
                {
                    YouTube = new YouTubeApiSettings
                    {
                        RefreshToken = _configuration["YouTubeApi:RefreshToken"] ?? ""
                    },
                    OpenAIKey = _configuration["OpenAI:ApiKey"] ?? _configuration["OpenAIKey"] ?? ""
                }
            };

            return Normalize(document);
        }

        private static EasyLotteryConfigDocument Normalize(EasyLotteryConfigDocument document)
        {
            document ??= new EasyLotteryConfigDocument();
            document.SystemSettings ??= new LotterySystemSettings();
            document.SystemSettings.YouTube ??= new YouTubeApiSettings();
            document.SystemSettings.DonationIntegration ??= new DonationIntegrationSettings();
            document.SystemSettings.MailDelivery ??= new MailDeliverySettings();
            document.SystemSettings.DonationIntegration.Ecpay ??= new DonationProviderSettings { Name = "綠界" };
            document.SystemSettings.DonationIntegration.NewebPay ??= new DonationProviderSettings { Name = "藍新" };
            document.SystemSettings.DonationIntegration.OenTw ??= new DonationProviderSettings { Name = "oen.tw" };
            document.SystemSettings.DonationIntegration.TwitchBits ??= new DonationProviderSettings { Name = "Twitch 小奇點" };
            document.SystemSettings.Audit ??= new AuditSettings();
            document.ObsLayout ??= new ObsLayoutSettings();
            document.ObsLayout.ActiveLayoutKey = ObsLayoutCatalog.NormalizeKey(document.ObsLayout.ActiveLayoutKey);
            document.IdSequence ??= new LotteryIdSequence();
            document.PokeTemplates ??= new List<PokeTemplate>();
            document.RouletteTemplates ??= new List<RouletteTemplate>();
            document.ActivityResults ??= new List<ActivityResultRecord>();
            document.DrawingRulePresets ??= new List<DrawingRulePreset>();
            document.DrawingRules ??= new DrawingRuleSettings();
            document.VisualStyle ??= new VisualStyleSettings();
            document.VisualStyle.ActiveThemeKey = VisualStyleCatalog.NormalizeKey(document.VisualStyle.ActiveThemeKey);
            document.SoundCue ??= new SoundCueSettings();
            document.SoundCue.ActivePresetKey = SoundCuePreset.NormalizeKey(document.SoundCue.ActivePresetKey);
            document.OvertimeOverlay ??= new OvertimeOverlaySettings();
            document.OvertimeOverlay.Title = document.OvertimeOverlay.Title.Trim();
            document.OvertimeOverlay.Subtitle = document.OvertimeOverlay.Subtitle.Trim();
            document.OvertimeOverlay.ThemeKey = string.IsNullOrWhiteSpace(document.OvertimeOverlay.ThemeKey)
                ? "festival-stage"
                : document.OvertimeOverlay.ThemeKey.Trim();
            document.OvertimeOverlay.MessageTemplateKey = OvertimeMessageTemplatePreset.NormalizeKey(document.OvertimeOverlay.MessageTemplateKey);
            document.OvertimeOverlay.TextAnimationKey = OvertimeTextAnimationPreset.NormalizeKey(document.OvertimeOverlay.TextAnimationKey);
            document.OvertimeOverlay.MaxVisibleItems = Math.Max(1, document.OvertimeOverlay.MaxVisibleItems);
            document.SystemSettings.ResultNotificationEmail = document.SystemSettings.ResultNotificationEmail.Trim();
            document.SystemSettings.DonationIntegration.Ecpay.Name = string.IsNullOrWhiteSpace(document.SystemSettings.DonationIntegration.Ecpay.Name) ? "綠界" : document.SystemSettings.DonationIntegration.Ecpay.Name.Trim();
            document.SystemSettings.DonationIntegration.NewebPay.Name = string.IsNullOrWhiteSpace(document.SystemSettings.DonationIntegration.NewebPay.Name) ? "藍新" : document.SystemSettings.DonationIntegration.NewebPay.Name.Trim();
            document.SystemSettings.DonationIntegration.OenTw.Name = string.IsNullOrWhiteSpace(document.SystemSettings.DonationIntegration.OenTw.Name) ? "oen.tw" : document.SystemSettings.DonationIntegration.OenTw.Name.Trim();
            document.SystemSettings.DonationIntegration.TwitchBits.Name = string.IsNullOrWhiteSpace(document.SystemSettings.DonationIntegration.TwitchBits.Name) ? "Twitch 小奇點" : document.SystemSettings.DonationIntegration.TwitchBits.Name.Trim();
            document.SystemSettings.MailDelivery.SmtpHost = document.SystemSettings.MailDelivery.SmtpHost.Trim();
            document.SystemSettings.MailDelivery.SmtpPort = Math.Max(0, document.SystemSettings.MailDelivery.SmtpPort);
            document.SystemSettings.MailDelivery.SmtpUsername = document.SystemSettings.MailDelivery.SmtpUsername.Trim();
            document.SystemSettings.MailDelivery.SmtpPassword = document.SystemSettings.MailDelivery.SmtpPassword.Trim();
            document.SystemSettings.MailDelivery.FromAddress = document.SystemSettings.MailDelivery.FromAddress.Trim();
            document.SystemSettings.MailDelivery.FromName = string.IsNullOrWhiteSpace(document.SystemSettings.MailDelivery.FromName) ? "EasyLottery" : document.SystemSettings.MailDelivery.FromName.Trim();
            document.SystemSettings.EnableYouTubeSuperChat &= document.SystemSettings.YouTube.HasConfiguration;
            document.SystemSettings.DonationIntegration.Ecpay.IsEnabled &= document.SystemSettings.DonationIntegration.Ecpay.HasConfiguration;
            document.SystemSettings.DonationIntegration.NewebPay.IsEnabled &= document.SystemSettings.DonationIntegration.NewebPay.HasConfiguration;
            document.SystemSettings.DonationIntegration.OenTw.IsEnabled &= document.SystemSettings.DonationIntegration.OenTw.HasConfiguration;
            document.SystemSettings.DonationIntegration.TwitchBits.IsEnabled &= document.SystemSettings.DonationIntegration.TwitchBits.HasConfiguration;
            document.OvertimeOverlay.DemoSuperChatAmount = Math.Max(0, document.OvertimeOverlay.DemoSuperChatAmount);
            document.OvertimeOverlay.DemoEcpayAmount = Math.Max(0, document.OvertimeOverlay.DemoEcpayAmount);
            document.OvertimeOverlay.SupportMessageVisibleSeconds = Math.Clamp(document.OvertimeOverlay.SupportMessageVisibleSeconds, 1, 60);
            document.OvertimeOverlay.StreamStartedAtUtc = NormalizeDateTimeOffset(document.OvertimeOverlay.StreamStartedAtUtc);
            document.OvertimeOverlay.PlannedEndAtUtc = NormalizeDateTimeOffset(document.OvertimeOverlay.PlannedEndAtUtc);
            document.OvertimeOverlay.RewardRules ??= new List<OvertimeRewardRule>();
            NormalizeOvertimeRewardRules(document.OvertimeOverlay.RewardRules);
            document.AuditRecords ??= new List<ChangeAuditRecord>();

            foreach (var template in document.PokeTemplates)
            {
                NormalizePokeTemplate(template);
            }

            foreach (var template in document.RouletteTemplates)
            {
                NormalizeRouletteTemplate(template);
            }

            foreach (var activityResult in document.ActivityResults)
            {
                NormalizeActivityResult(activityResult);
            }

            foreach (var preset in document.DrawingRulePresets)
            {
                NormalizeDrawingRulePreset(preset);
            }

            foreach (var auditRecord in document.AuditRecords)
            {
                NormalizeAuditRecord(auditRecord);
            }

            NormalizeDrawingRuleSettings(document.DrawingRules);

            document.IdSequence.NextPokeTemplateId = Math.Max(document.IdSequence.NextPokeTemplateId, document.PokeTemplates.Select(t => t.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextPokeCellId = Math.Max(document.IdSequence.NextPokeCellId, document.PokeTemplates.SelectMany(t => t.Cells).Select(c => c.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextRouletteTemplateId = Math.Max(document.IdSequence.NextRouletteTemplateId, document.RouletteTemplates.Select(t => t.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextRouletteSegmentId = Math.Max(document.IdSequence.NextRouletteSegmentId, document.RouletteTemplates.SelectMany(t => t.Segments).Select(s => s.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextActivityResultId = Math.Max(document.IdSequence.NextActivityResultId, document.ActivityResults.Select(result => result.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextAuditRecordId = Math.Max(document.IdSequence.NextAuditRecordId, document.AuditRecords.Select(record => record.Id).DefaultIfEmpty(0).Max() + 1);
            document.SystemSettings.Audit.ActorName = string.IsNullOrWhiteSpace(document.SystemSettings.Audit.ActorName) ? "本機操作" : document.SystemSettings.Audit.ActorName.Trim();
            document.SystemSettings.ResultNotificationEmail = document.SystemSettings.ResultNotificationEmail.Trim();

            return document;
        }

        private static EasyLotteryConfigDocument CloneDocument(EasyLotteryConfigDocument document)
        {
            var json = JsonSerializer.Serialize(document, CloneSerializerOptions);
            var clone = JsonSerializer.Deserialize<EasyLotteryConfigDocument>(json, CloneSerializerOptions)
                ?? new EasyLotteryConfigDocument();

            return Normalize(clone);
        }

        private static void NormalizePokeTemplate(PokeTemplate template)
        {
            template.Name ??= "";
            template.Description ??= "";
            template.BackgroundImageUrl ??= "";
            template.FontFamily ??= "";
            template.CongratulationMessage ??= "";
            template.PokeSoundUrl ??= "";
            template.OpenSoundUrl ??= "";
            template.Cells ??= new List<PokeCell>();

            var orderedCells = template.Cells.OrderBy(c => c.Index).ToList();
            for (var index = 0; index < orderedCells.Count; index++)
            {
                var cell = orderedCells[index];
                cell.Index = index;
                cell.Title ??= "";
                cell.SubTitle ??= "";
                cell.ImageUrl ??= "";
                cell.RevealedImageUrl ??= "";
                cell.RevealedColor ??= "#cccccc";
                cell.TemplateId = template.Id;
                cell.Template = null!;
            }

            template.Cells = orderedCells;
        }

        private static void NormalizeRouletteTemplate(RouletteTemplate template)
        {
            template.Name ??= "";
            template.Description ??= "";
            template.EasingFunction ??= "ease-out-cubic";
            template.CenterImageUrl ??= "";
            template.BackgroundImageUrl ??= "";
            template.PointerImageUrl ??= "";
            template.SpinSoundUrl ??= "";
            template.WinSoundUrl ??= "";
            template.Segments ??= new List<RouletteSegment>();

            var orderedSegments = template.Segments.OrderBy(s => s.Index).ToList();
            for (var index = 0; index < orderedSegments.Count; index++)
            {
                var segment = orderedSegments[index];
                segment.Index = index;
                segment.Title ??= "";
                segment.ImageUrl ??= "";
                segment.Color ??= "#cccccc";
                segment.TemplateId = template.Id;
                segment.Template = null!;
            }

            template.Segments = orderedSegments;
        }

        private static void NormalizeActivityResult(ActivityResultRecord activityResult)
        {
            activityResult.ActivityName ??= "";
            activityResult.Summary ??= "";
            activityResult.Items ??= new List<ActivityResultItem>();

            var orderedItems = activityResult.Items.OrderBy(item => item.Order).ToList();
            for (var index = 0; index < orderedItems.Count; index++)
            {
                var item = orderedItems[index];
                item.Order = index + 1;
                item.Name ??= "";
                item.Description ??= "";
                item.ImageUrl ??= "";
                item.Color ??= "";
            }

            activityResult.Items = orderedItems;
        }

        private static void NormalizeDrawingRulePreset(DrawingRulePreset preset)
        {
            preset.Name ??= "";
            preset.Description ??= "";
            preset.LevelRates ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in preset.LevelRates.Keys.ToList())
            {
                preset.LevelRates[key] = Math.Max(1, preset.LevelRates[key]);
            }
        }

        private static void NormalizeDrawingRuleSettings(DrawingRuleSettings settings)
        {
            settings.LevelRates ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in settings.LevelRates.Keys.ToList())
            {
                settings.LevelRates[key] = Math.Max(1, settings.LevelRates[key]);
            }
        }

        private static void NormalizeOvertimeRewardRules(List<OvertimeRewardRule> rules)
        {
            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index] ?? new OvertimeRewardRule();
                rule.Label = rule.Label?.Trim() ?? "";
                rule.AmountThreshold = Math.Max(0, rule.AmountThreshold);
                rule.AddHours = Math.Max(0, rule.AddHours);
                rule.AddMinutes = Math.Max(0, rule.AddMinutes);
                rules[index] = rule;
            }
        }

        private static DateTimeOffset? NormalizeDateTimeOffset(DateTimeOffset? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var utcValue = value.Value.ToUniversalTime();
            if (utcValue.Year < 2000)
            {
                return null;
            }

            return utcValue;
        }

        private static void NormalizeAuditRecord(ChangeAuditRecord auditRecord)
        {
            auditRecord.ChangedBy ??= "本機操作";
            auditRecord.Category ??= "";
            auditRecord.Action ??= "";
            auditRecord.TargetName ??= "";
            auditRecord.Details ??= "";
        }
    }
}
