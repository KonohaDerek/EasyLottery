using EasyLotteryDomain.Models.Config;
using YamlDotNet.Serialization;
using EasyLotteryDomain.Services;

namespace EasyLotteryApi;

/// <summary>
/// Keeps payment secrets in the server-side YAML while allowing the non-secret
/// configuration to remain shared by every browser connected to this host.
/// </summary>
public sealed class ConfigSecretRedactor
{
    public const string UnchangedSecretMask = "__EASYLOTTERY_SECRET_UNCHANGED__";

    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();

    public string RedactForBrowser(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return yaml;
        }

        var document = Deserialize(yaml);
        Redact(document.SystemSettings?.DonationIntegration);
        RedactSystemSecrets(document.SystemSettings);
        return _serializer.Serialize(document);
    }

    public string NormalizeForPersistence(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return yaml;
        }

        var document = Deserialize(yaml);
        MigrateLegacySettings(document.SystemSettings?.DonationIntegration);
        return _serializer.Serialize(document);
    }

    public string MergeBrowserUpdate(string existingYaml, string submittedYaml)
    {
        var existing = string.IsNullOrWhiteSpace(existingYaml)
            ? new EasyLotteryConfigDocument()
            : Deserialize(existingYaml);
        var submitted = string.IsNullOrWhiteSpace(submittedYaml)
            ? new EasyLotteryConfigDocument()
            : Deserialize(submittedYaml);

        MigrateLegacySettings(existing.SystemSettings?.DonationIntegration);
        MigrateLegacySettings(submitted.SystemSettings?.DonationIntegration);
        MergeSecrets(existing.SystemSettings?.DonationIntegration, submitted.SystemSettings?.DonationIntegration);
        MergeSystemSecrets(existing.SystemSettings, submitted.SystemSettings);
        return _serializer.Serialize(submitted);
    }

    private EasyLotteryConfigDocument Deserialize(string yaml) =>
        _deserializer.Deserialize<EasyLotteryConfigDocument>(yaml) ?? new EasyLotteryConfigDocument();

    private static void Redact(DonationIntegrationSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        Redact(settings.Ecpay);
        Redact(settings.NewebPay);
        Redact(settings.OenTw);
        Redact(settings.TwitchBits);
    }

    private static void RedactSystemSecrets(LotterySystemSettings? settings)
    {
        if (settings is null) return;
        settings.YouTube.ApiKey = ToMask(settings.YouTube.ApiKey);
        settings.OpenAIKey = ToMask(settings.OpenAIKey);
        settings.MailDelivery.SmtpPassword = ToMask(settings.MailDelivery.SmtpPassword);
    }

    private static void MergeSystemSecrets(LotterySystemSettings? existing, LotterySystemSettings? submitted)
    {
        if (existing is null || submitted is null) return;
        submitted.YouTube.ApiKey = PreserveIfMasked(submitted.YouTube.ApiKey, existing.YouTube.ApiKey);
        submitted.OpenAIKey = PreserveIfMasked(submitted.OpenAIKey, existing.OpenAIKey);
        submitted.MailDelivery.SmtpPassword = PreserveIfMasked(submitted.MailDelivery.SmtpPassword, existing.MailDelivery.SmtpPassword);
    }

    private static void MigrateLegacySettings(DonationIntegrationSettings? settings)
    {
        settings?.Ecpay.MigrateLegacyConfiguration();
        settings?.NewebPay.MigrateLegacyConfiguration();
        settings?.OenTw.MigrateLegacyConfiguration();
        settings?.TwitchBits.MigrateLegacyConfiguration();
    }

    private static void Redact(DonationProviderSettings? provider)
    {
        if (provider is null)
        {
            return;
        }

        provider.ApiKey = ToMask(provider.ApiKey);
        provider.SecretKey = ToMask(provider.SecretKey);
        provider.AccessToken = ToMask(provider.AccessToken);
        Redact(provider.Testing);
        Redact(provider.Production);
    }

    private static void Redact(DonationProviderConnectionSettings? provider)
    {
        if (provider is null) return;
        provider.ApiKey = ToMask(provider.ApiKey);
        provider.SecretKey = ToMask(provider.SecretKey);
        provider.AccessToken = ToMask(provider.AccessToken);
    }

    private static string ToMask(string value) =>
        string.IsNullOrWhiteSpace(value) ? "" : UnchangedSecretMask;

    private static void MergeSecrets(DonationIntegrationSettings? existing, DonationIntegrationSettings? submitted)
    {
        if (existing is null || submitted is null)
        {
            return;
        }

        MergeSecrets(existing.Ecpay, submitted.Ecpay);
        MergeSecrets(existing.NewebPay, submitted.NewebPay);
        MergeSecrets(existing.OenTw, submitted.OenTw);
        MergeSecrets(existing.TwitchBits, submitted.TwitchBits);
    }

    private static void MergeSecrets(DonationProviderSettings? existing, DonationProviderSettings? submitted)
    {
        if (existing is null || submitted is null)
        {
            return;
        }

        submitted.ApiKey = PreserveIfMasked(submitted.ApiKey, existing.ApiKey);
        submitted.SecretKey = PreserveIfMasked(submitted.SecretKey, existing.SecretKey);
        submitted.AccessToken = PreserveIfMasked(submitted.AccessToken, existing.AccessToken);
        MergeSecrets(existing.Testing, submitted.Testing);
        MergeSecrets(existing.Production, submitted.Production);
    }

    private static void MergeSecrets(DonationProviderConnectionSettings? existing, DonationProviderConnectionSettings? submitted)
    {
        if (existing is null || submitted is null) return;
        submitted.ApiKey = PreserveIfMasked(submitted.ApiKey, existing.ApiKey);
        submitted.SecretKey = PreserveIfMasked(submitted.SecretKey, existing.SecretKey);
        submitted.AccessToken = PreserveIfMasked(submitted.AccessToken, existing.AccessToken);
    }

    private static string PreserveIfMasked(string submitted, string existing) =>
        string.Equals(submitted, UnchangedSecretMask, StringComparison.Ordinal) ? existing : submitted;
}
