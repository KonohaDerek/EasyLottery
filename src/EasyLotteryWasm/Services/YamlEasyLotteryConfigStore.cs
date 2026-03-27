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
                if (_cachedDocument != null)
                {
                    return CloneDocument(_cachedDocument);
                }

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
            document.IdSequence ??= new LotteryIdSequence();
            document.PokeTemplates ??= new List<PokeTemplate>();
            document.RouletteTemplates ??= new List<RouletteTemplate>();
            document.ActivityResults ??= new List<ActivityResultRecord>();
            document.DrawingRulePresets ??= new List<DrawingRulePreset>();
            document.DrawingRules ??= new DrawingRuleSettings();
            document.VisualStyle ??= new VisualStyleSettings();
            document.VisualStyle.ActiveThemeKey = VisualStyleCatalog.NormalizeKey(document.VisualStyle.ActiveThemeKey);
            document.OvertimeOverlay ??= new OvertimeOverlaySettings();
            document.OvertimeOverlay.Title = document.OvertimeOverlay.Title.Trim();
            document.OvertimeOverlay.Subtitle = document.OvertimeOverlay.Subtitle.Trim();
            document.OvertimeOverlay.ThemeKey = string.IsNullOrWhiteSpace(document.OvertimeOverlay.ThemeKey)
                ? "festival-stage"
                : document.OvertimeOverlay.ThemeKey.Trim();
            document.OvertimeOverlay.MaxVisibleItems = Math.Max(1, document.OvertimeOverlay.MaxVisibleItems);

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

            NormalizeDrawingRuleSettings(document.DrawingRules);

            document.IdSequence.NextPokeTemplateId = Math.Max(document.IdSequence.NextPokeTemplateId, document.PokeTemplates.Select(t => t.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextPokeCellId = Math.Max(document.IdSequence.NextPokeCellId, document.PokeTemplates.SelectMany(t => t.Cells).Select(c => c.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextRouletteTemplateId = Math.Max(document.IdSequence.NextRouletteTemplateId, document.RouletteTemplates.Select(t => t.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextRouletteSegmentId = Math.Max(document.IdSequence.NextRouletteSegmentId, document.RouletteTemplates.SelectMany(t => t.Segments).Select(s => s.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextActivityResultId = Math.Max(document.IdSequence.NextActivityResultId, document.ActivityResults.Select(result => result.Id).DefaultIfEmpty(0).Max() + 1);

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
    }
}
