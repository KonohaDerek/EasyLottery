using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Settings;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class ConfigSecretRedactorTests
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    [TestMethod]
    public void RedactForBrowser_ReplacesEachPaymentSecret()
    {
        var redactor = new ConfigSecretRedactor();
        var yaml = _serializer.Serialize(CreateDocument());

        var browserDocument = _deserializer.Deserialize<EasyLotteryConfigDocument>(redactor.RedactForBrowser(yaml));

        Assert.AreEqual(ConfigSecretRedactor.UnchangedSecretMask, browserDocument.SystemSettings.DonationIntegration.Ecpay.ApiKey);
        Assert.AreEqual(ConfigSecretRedactor.UnchangedSecretMask, browserDocument.SystemSettings.DonationIntegration.Ecpay.SecretKey);
        Assert.AreEqual(ConfigSecretRedactor.UnchangedSecretMask, browserDocument.SystemSettings.DonationIntegration.OenTw.AccessToken);
        Assert.AreEqual("merchant-id", browserDocument.SystemSettings.DonationIntegration.Ecpay.MerchantId);
        Assert.AreEqual(ConfigSecretRedactor.UnchangedSecretMask, browserDocument.SystemSettings.YouTube.ApiKey);
        Assert.AreEqual(ConfigSecretRedactor.UnchangedSecretMask, browserDocument.SystemSettings.OpenAIKey);
        Assert.AreEqual(ConfigSecretRedactor.UnchangedSecretMask, browserDocument.SystemSettings.MailDelivery.SmtpPassword);
    }

    [TestMethod]
    public void MergeBrowserUpdate_PreservesMaskedSecretAndAcceptsReplacement()
    {
        var redactor = new ConfigSecretRedactor();
        var existing = _serializer.Serialize(CreateDocument());
        var browserDocument = _deserializer.Deserialize<EasyLotteryConfigDocument>(redactor.RedactForBrowser(existing));
        browserDocument.SystemSettings.DonationIntegration.Ecpay.SecretKey = "replacement-secret";

        var saved = _deserializer.Deserialize<EasyLotteryConfigDocument>(redactor.MergeBrowserUpdate(existing, _serializer.Serialize(browserDocument)));

        Assert.AreEqual("api-key", saved.SystemSettings.DonationIntegration.Ecpay.Testing.ApiKey);
        Assert.AreEqual("replacement-secret", saved.SystemSettings.DonationIntegration.Ecpay.Testing.SecretKey);
        Assert.AreEqual("access-token", saved.SystemSettings.DonationIntegration.OenTw.Testing.AccessToken);
        Assert.AreEqual("youtube-key", saved.SystemSettings.YouTube.ApiKey);
        Assert.AreEqual("openai-key", saved.SystemSettings.OpenAIKey);
        Assert.AreEqual("smtp-password", saved.SystemSettings.MailDelivery.SmtpPassword);
    }

    private static EasyLotteryConfigDocument CreateDocument() => new()
    {
        SystemSettings = new LotterySystemSettings
        {
            DonationIntegration = new DonationIntegrationSettings
            {
                Ecpay = new DonationProviderSettings { MerchantId = "merchant-id", ApiKey = "api-key", SecretKey = "secret-key" },
                OenTw = new DonationProviderSettings { AccessToken = "access-token" }
            },
            YouTube = new YouTubeApiSettings { ApiKey = "youtube-key" },
            OpenAIKey = "openai-key",
            MailDelivery = new MailDeliverySettings { SmtpPassword = "smtp-password" }
        }
    };
}
