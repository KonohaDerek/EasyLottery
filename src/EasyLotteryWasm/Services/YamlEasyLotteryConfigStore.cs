using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;
using Microsoft.JSInterop;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EasyLotteryWasm.Services
{
    public sealed class YamlEasyLotteryConfigStore : IEasyLotteryConfigStore
    {
        private readonly IConfiguration _configuration;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<YamlEasyLotteryConfigStore> _logger;
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;

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

        public async Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default)
        {
            var normalized = Normalize(document);
            var yaml = _serializer.Serialize(normalized);
            await _jsRuntime.InvokeVoidAsync("easyLotteryConfig.write", cancellationToken, yaml);
        }

        private EasyLotteryConfigDocument CreateDefaultDocument()
        {
            var document = new EasyLotteryConfigDocument
            {
                SystemSettings = new LotterySystemSettings
                {
                    YouTube = new YouTubeApiSettings
                    {
                        ApiKey = _configuration["YouTubeApi:ApiKey"] ?? "",
                        CredentialsBase64 = _configuration["YouTubeApi:CredentialsBase64"] ?? "",
                        RedirectUri = _configuration["YouTubeApi:RedirectUri"] ?? "",
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

            foreach (var template in document.PokeTemplates)
            {
                NormalizePokeTemplate(template);
            }

            foreach (var template in document.RouletteTemplates)
            {
                NormalizeRouletteTemplate(template);
            }

            document.IdSequence.NextPokeTemplateId = Math.Max(document.IdSequence.NextPokeTemplateId, document.PokeTemplates.Select(t => t.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextPokeCellId = Math.Max(document.IdSequence.NextPokeCellId, document.PokeTemplates.SelectMany(t => t.Cells).Select(c => c.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextRouletteTemplateId = Math.Max(document.IdSequence.NextRouletteTemplateId, document.RouletteTemplates.Select(t => t.Id).DefaultIfEmpty(0).Max() + 1);
            document.IdSequence.NextRouletteSegmentId = Math.Max(document.IdSequence.NextRouletteSegmentId, document.RouletteTemplates.SelectMany(t => t.Segments).Select(s => s.Id).DefaultIfEmpty(0).Max() + 1);

            return document;
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
    }
}