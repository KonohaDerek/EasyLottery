using EasyLotteryDomain.Models.Entities;
using YamlDotNet.Serialization;

namespace EasyLotteryDomain.Models.Config
{
    public sealed class EasyLotteryConfigDocument
    {
        public int ConfigVersion { get; set; } = YamlDocumentSchema.CurrentVersion;

        public LotterySystemSettings SystemSettings { get; set; } = new();

        public LotteryIdSequence IdSequence { get; set; } = new();

        public List<PokeTemplate> PokeTemplates { get; set; } = new();

        public List<RouletteTemplate> RouletteTemplates { get; set; } = new();

        public List<ActivityResultRecord> ActivityResults { get; set; } = new();

        public List<DrawingRulePreset> DrawingRulePresets { get; set; } = new();

        public DrawingRuleSettings DrawingRules { get; set; } = new();
        public List<DonateLotteryActivity> DonateLotteryActivities { get; set; } = [];
        public List<DonateLotteryDrawRecord> DonateLotteryDrawRecords { get; set; } = [];
        public List<string> ProcessedDonatePaymentIds { get; set; } = [];
        public VisualStyleSettings VisualStyle { get; set; } = new();
        public ObsLayoutSettings ObsLayout { get; set; } = new();
        public SoundCueSettings SoundCue { get; set; } = new();
        public OvertimeOverlaySettings OvertimeOverlay { get; set; } = new();
        public List<ChangeAuditRecord> AuditRecords { get; set; } = new();
    }

    public sealed class LotterySystemSettings
    {
        public YouTubeApiSettings YouTube { get; set; } = new();

        public DonationIntegrationSettings DonationIntegration { get; set; } = new();

        public PublicCallbackSettings PublicCallback { get; set; } = new();

        public MailDeliverySettings MailDelivery { get; set; } = new();

        public bool EnableYouTubeSuperChat { get; set; }

        public string ResultNotificationEmail { get; set; } = "";

        public string OpenAIKey { get; set; } = "";

        public AuditSettings Audit { get; set; } = new();
    }

    public sealed class YouTubeApiSettings
    {
        public string ApiKey { get; set; } = "";

        [YamlIgnore]
        public bool HasConfiguration =>
            !string.IsNullOrWhiteSpace(ApiKey);
    }

    public sealed class ObsLayoutSettings
    {
        public string ActiveLayoutKey { get; set; } = "stage-spotlight";
    }

    public sealed class SoundCueSettings
    {
        public string ActivePresetKey { get; set; } = "arcade-stage";
    }

    public sealed class DonationIntegrationSettings
    {
        public DonationProviderSettings Ecpay { get; set; } = new()
        {
            Name = "綠界"
        };

        public DonationProviderSettings NewebPay { get; set; } = new()
        {
            Name = "藍新"
        };

        public DonationProviderSettings OenTw { get; set; } = new()
        {
            Name = "oen.tw"
        };

        public DonationProviderSettings TwitchBits { get; set; } = new()
        {
            Name = "Twitch 小奇點"
        };
    }

    public static class PaymentProviderIds
    {
        public const string EcpayBroadcaster = "ecpay-broadcaster";
        public const string NewebPayDonation = "newebpay-donation";
        public const string Oen = "oen";
    }

    public static class PaymentProviderEnvironments
    {
        public const string Testing = "testing";
        public const string Production = "production";

        public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            Production => Production,
            _ => Testing
        };
    }

    public sealed record PaymentProviderDescriptor(
        string Id,
        string DisplayName,
        string CallbackPath,
        string DocumentationUrl,
        string MarketPackageId);

    public static class PaymentProviderCatalog
    {
        public static IReadOnlyList<PaymentProviderDescriptor> BuiltIns { get; } =
        [
            new(PaymentProviderIds.EcpayBroadcaster, "綠界直播主收款", "/api/payments/ecpay/notify", "https://developers.ecpay.com.tw/?p=41030", "payment.ecpay-broadcaster"),
            new(PaymentProviderIds.NewebPayDonation, "藍新捐款平台", "/api/payments/newebpay/notify", "https://donation.newebpay.com/doc/newebpay_DONATE_1_0_2.pdf", "payment.newebpay-donation"),
            new(PaymentProviderIds.Oen, "oen.tw 應援金流", "/api/payments/oen/notify", "https://github.com/OEN-Tech/oen-payment-skill", "payment.oen")
        ];
    }

    public sealed class PublicCallbackSettings
    {
        public string CustomDomain { get; set; } = "";

        public string TunnelProvider { get; set; } = PublicCallbackTunnelProviders.CloudflareQuick;

        public string ActivePublicBaseUrl { get; set; } = "";

        public string GetConfiguredBaseUrl()
        {
            return string.IsNullOrWhiteSpace(CustomDomain)
                ? ActivePublicBaseUrl.Trim()
                : CustomDomain.Trim();
        }
    }

    public static class PublicCallbackTunnelProviders
    {
        public const string CloudflareQuick = "cloudflare-quick";
        public const string DevTunnels = "dev-tunnels";

        public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            DevTunnels => DevTunnels,
            _ => CloudflareQuick
        };
    }

    public sealed class DonationProviderSettings
    {
        public string Name { get; set; } = "";

        public bool IsEnabled { get; set; }

        public string Environment { get; set; } = PaymentProviderEnvironments.Testing;

        public string MerchantId { get; set; } = "";

        public string ApiKey { get; set; } = "";

        public string SecretKey { get; set; } = "";

        public string ChannelId { get; set; } = "";

        public string AccessToken { get; set; } = "";

        public string CreatorId { get; set; } = "";

        /// <summary>
        /// The provider-specific donation page for a sandbox transaction. It is
        /// configurable because providers issue the final page path per merchant.
        /// </summary>
        public string TestingDonationUrl { get; set; } = "";

        // Environment-specific settings. The original top-level fields are kept
        // for migration from existing YAML files and act as the active runtime view.
        public DonationProviderConnectionSettings Testing { get; set; } = new();
        public DonationProviderConnectionSettings Production { get; set; } = new();

        private bool HasLegacyConfiguration =>
            !string.IsNullOrWhiteSpace(MerchantId) ||
            !string.IsNullOrWhiteSpace(ApiKey) ||
            !string.IsNullOrWhiteSpace(SecretKey) ||
            !string.IsNullOrWhiteSpace(ChannelId) ||
            !string.IsNullOrWhiteSpace(AccessToken) ||
            !string.IsNullOrWhiteSpace(CreatorId);

        [YamlIgnore]
        public bool HasConfiguration => GetActiveConnection().HasConfiguration;

        public DonationProviderConnectionSettings GetActiveConnection() =>
            PaymentProviderEnvironments.Normalize(Environment) == PaymentProviderEnvironments.Production ? Production : Testing;

        public void ApplyActiveConnection()
        {
            var source = GetActiveConnection();
            IsEnabled = source.IsEnabled;
            MerchantId = source.MerchantId;
            ApiKey = source.ApiKey;
            SecretKey = source.SecretKey;
            ChannelId = source.ChannelId;
            AccessToken = source.AccessToken;
            CreatorId = source.CreatorId;
            TestingDonationUrl = source.DonationPageUrl;
        }

        public void MigrateLegacyConfiguration()
        {
            Testing ??= new DonationProviderConnectionSettings();
            Production ??= new DonationProviderConnectionSettings();
            if (ReferenceEquals(Testing, Production))
            {
                Production = new DonationProviderConnectionSettings();
            }
            if (!Testing.HasConfiguration && !Production.HasConfiguration && HasLegacyConfiguration)
            {
                var target = GetActiveConnection();
                target.IsEnabled = IsEnabled;
                target.MerchantId = MerchantId;
                target.ApiKey = ApiKey;
                target.SecretKey = SecretKey;
                target.ChannelId = ChannelId;
                target.AccessToken = AccessToken;
                target.CreatorId = CreatorId;
                target.DonationPageUrl = TestingDonationUrl;
            }

            // Do not persist duplicate active credentials at the top level.
            IsEnabled = false;
            MerchantId = ApiKey = SecretKey = ChannelId = AccessToken = CreatorId = TestingDonationUrl = null!;
        }
    }

    public sealed class DonationProviderConnectionSettings
    {
        public bool IsEnabled { get; set; }
        public string MerchantId { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
        public string ChannelId { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string CreatorId { get; set; } = "";
        public string DonationPageUrl { get; set; } = "";
        [YamlIgnore]
        public bool HasConfiguration => !string.IsNullOrWhiteSpace(MerchantId) || !string.IsNullOrWhiteSpace(ApiKey) || !string.IsNullOrWhiteSpace(SecretKey) || !string.IsNullOrWhiteSpace(ChannelId) || !string.IsNullOrWhiteSpace(AccessToken) || !string.IsNullOrWhiteSpace(CreatorId);
    }

    public sealed class MailDeliverySettings
    {
        public string SmtpHost { get; set; } = "";

        public int SmtpPort { get; set; } = 587;

        public string SmtpUsername { get; set; } = "";

        public string SmtpPassword { get; set; } = "";

        public string FromAddress { get; set; } = "";

        public string FromName { get; set; } = "EasyLottery";

        public bool EnableSsl { get; set; } = true;

        [YamlIgnore]
        public bool HasConfiguration =>
            !string.IsNullOrWhiteSpace(SmtpHost) &&
            SmtpPort > 0 &&
            !string.IsNullOrWhiteSpace(FromAddress);
    }

    public sealed class LotteryIdSequence
    {
        public int NextPokeTemplateId { get; set; } = 1;

        public int NextPokeCellId { get; set; } = 1;

        public int NextRouletteTemplateId { get; set; } = 1;

        public int NextRouletteSegmentId { get; set; } = 1;

        public int NextActivityResultId { get; set; } = 1;

        public int NextAuditRecordId { get; set; } = 1;

        public int NextDonateLotteryActivityId { get; set; } = 1;
        public int NextDonateLotteryPrizeId { get; set; } = 1;
        public int NextDonateLotteryDrawRecordId { get; set; } = 1;
    }

    public sealed class DrawingRuleSettings
    {
        public Dictionary<string, int> LevelRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class AuditSettings
    {
        public string ActorName { get; set; } = "本機操作";
    }

    public sealed class ChangeAuditRecord
    {
        public int Id { get; set; }

        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

        public string ChangedBy { get; set; } = "本機操作";

        public string Category { get; set; } = "";

        public string Action { get; set; } = "";

        public string TargetName { get; set; } = "";

        public string Details { get; set; } = "";

        public string? BeforeJson { get; set; }

        public string? AfterJson { get; set; }
    }
}
