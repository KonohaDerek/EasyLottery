using System.Security.Cryptography;
using System.Text;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.Settings;

public sealed class SqliteSettingsSectionRepository(
    IEasyLotteryConfigStore store,
    ConfigSecretRedactor secrets) : ISettingsSectionRepository
{
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();

    public Task<SettingsSectionSnapshot<ObsLayoutSettings>> ReadObsLayoutAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(document => document.ObsLayout ?? new ObsLayoutSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<ObsLayoutSettings>> UpdateObsLayoutAsync(ObsLayoutSettings value, string? expectedETag, CancellationToken cancellationToken = default) =>
        UpdateAsync(document => document.ObsLayout = value, expectedETag, document => document.ObsLayout ?? new ObsLayoutSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<SoundCueSettings>> ReadSoundCueAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(document => document.SoundCue ?? new SoundCueSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<SoundCueSettings>> UpdateSoundCueAsync(SoundCueSettings value, string? expectedETag, CancellationToken cancellationToken = default) =>
        UpdateAsync(document => document.SoundCue = value, expectedETag, document => document.SoundCue ?? new SoundCueSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<VisualStyleSettings>> ReadVisualStyleAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(document => document.VisualStyle ?? new VisualStyleSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<VisualStyleSettings>> UpdateVisualStyleAsync(VisualStyleSettings value, string? expectedETag, CancellationToken cancellationToken = default) =>
        UpdateAsync(document => document.VisualStyle = value, expectedETag, document => document.VisualStyle ?? new VisualStyleSettings(), cancellationToken);

    public async Task<SettingsSectionSnapshot<PaymentSettingsResource>> ReadPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var document = await store.LoadAsync(cancellationToken);
        return new SettingsSectionSnapshot<PaymentSettingsResource>(
            secrets.RedactPaymentResource(document.SystemSettings ?? new LotterySystemSettings()),
            ComputeETag(document));
    }

    public async Task<SettingsSectionSnapshot<PaymentSettingsResource>> UpdatePaymentsAsync(PaymentSettingsResource value, string? expectedETag, CancellationToken cancellationToken = default)
    {
        var document = await store.LoadAsync(cancellationToken);
        EnsureETag(expectedETag, document);
        var existing = document.SystemSettings ?? new LotterySystemSettings();
        document.SystemSettings ??= new LotterySystemSettings();
        var merged = secrets.MergePaymentResource(existing, value);
        document.SystemSettings.DonationIntegration = merged.DonationIntegration;
        document.SystemSettings.PublicCallback = merged.PublicCallback;
        document.SystemSettings.MailDelivery = merged.MailDelivery;
        document.SystemSettings.YouTube = merged.YouTube;
        document.SystemSettings.EnableYouTubeSuperChat = merged.EnableYouTubeSuperChat;
        document.SystemSettings.ResultNotificationEmail = merged.ResultNotificationEmail;
        await store.SaveAsync(document, cancellationToken);
        return new SettingsSectionSnapshot<PaymentSettingsResource>(
            secrets.RedactPaymentResource(document.SystemSettings),
            ComputeETag(document));
    }

    public Task<SettingsSectionSnapshot<OvertimeOverlaySettings>> ReadOvertimeAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(document => document.OvertimeOverlay ?? new OvertimeOverlaySettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<OvertimeOverlaySettings>> UpdateOvertimeAsync(OvertimeOverlaySettings value, string? expectedETag, CancellationToken cancellationToken = default) =>
        UpdateAsync(document => document.OvertimeOverlay = value, expectedETag, document => document.OvertimeOverlay ?? new OvertimeOverlaySettings(), cancellationToken);

    private async Task<SettingsSectionSnapshot<T>> ReadAsync<T>(Func<EasyLotteryConfigDocument, T> selector, CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        return new SettingsSectionSnapshot<T>(selector(document), ComputeETag(document));
    }

    private async Task<SettingsSectionSnapshot<T>> UpdateAsync<T>(
        Action<EasyLotteryConfigDocument> assign,
        string? expectedETag,
        Func<EasyLotteryConfigDocument, T> selector,
        CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        EnsureETag(expectedETag, document);
        assign(document);
        await store.SaveAsync(document, cancellationToken);
        return new SettingsSectionSnapshot<T>(selector(document), ComputeETag(document));
    }

    private void EnsureETag(string? expectedETag, EasyLotteryConfigDocument document)
    {
        if (string.IsNullOrWhiteSpace(expectedETag) || expectedETag == "*") return;
        var actual = ComputeETag(document);
        if (!string.Equals(expectedETag.Trim(), actual, StringComparison.Ordinal))
            throw new ConfigurationConcurrencyException(expectedETag, actual);
    }

    private string ComputeETag(EasyLotteryConfigDocument document)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(_serializer.Serialize(document)));
        return $"\"{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}\"";
    }
}
