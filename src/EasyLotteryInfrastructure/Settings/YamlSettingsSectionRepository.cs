using System.Security.Cryptography;
using System.Text;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Storage;

namespace EasyLotteryInfrastructure.Settings;

/// <summary>
/// Stores one settings resource at a time. This avoids using the compatibility
/// /settings document as the write path and lets unrelated settings evolve
/// independently while retaining the existing split YAML files.
/// </summary>
public sealed class YamlSettingsSectionRepository : ISettingsSectionRepository
{
    private readonly YamlSettingsDocumentRepository _repository;
    private readonly IStorageGateProvider _storageGates;
    private readonly ConfigSecretRedactor _secrets;

    public YamlSettingsSectionRepository(
        YamlSettingsDocumentRepository repository,
        IStorageGateProvider storageGates,
        ConfigSecretRedactor secrets)
    {
        _repository = repository;
        _storageGates = storageGates;
        _secrets = secrets;
    }

    public Task<SettingsSectionSnapshot<ObsLayoutSettings>> ReadObsLayoutAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(document => document.ObsLayout ?? new ObsLayoutSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<ObsLayoutSettings>> UpdateObsLayoutAsync(ObsLayoutSettings value, string? expectedETag, CancellationToken cancellationToken = default) =>
        UpdateAsync((document, _) => document.ObsLayout = value, expectedETag, document => document.ObsLayout ?? new ObsLayoutSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<SoundCueSettings>> ReadSoundCueAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(document => document.SoundCue ?? new SoundCueSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<SoundCueSettings>> UpdateSoundCueAsync(SoundCueSettings value, string? expectedETag, CancellationToken cancellationToken = default) =>
        UpdateAsync((document, _) => document.SoundCue = value, expectedETag, document => document.SoundCue ?? new SoundCueSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<VisualStyleSettings>> ReadVisualStyleAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(document => document.VisualStyle ?? new VisualStyleSettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<VisualStyleSettings>> UpdateVisualStyleAsync(VisualStyleSettings value, string? expectedETag, CancellationToken cancellationToken = default) =>
        UpdateAsync((document, _) => document.VisualStyle = value, expectedETag, document => document.VisualStyle ?? new VisualStyleSettings(), cancellationToken);

    public async Task<SettingsSectionSnapshot<PaymentSettingsResource>> ReadPaymentsAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _repository.StoragePath);
        var document = await _repository.ReadUnsafeAsync(cancellationToken);
        return new SettingsSectionSnapshot<PaymentSettingsResource>(
            _secrets.RedactPaymentResource(document.SystemSettings ?? new LotterySystemSettings()),
            ComputeETag());
    }

    public async Task<SettingsSectionSnapshot<PaymentSettingsResource>> UpdatePaymentsAsync(PaymentSettingsResource value, string? expectedETag, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _repository.StoragePath);
        var document = await _repository.ReadUnsafeAsync(cancellationToken);
        EnsureETag(expectedETag);
        var existing = document.SystemSettings ?? new LotterySystemSettings();
        document.SystemSettings ??= new LotterySystemSettings();
        var merged = _secrets.MergePaymentResource(existing, value);
        document.SystemSettings.DonationIntegration = merged.DonationIntegration;
        document.SystemSettings.PublicCallback = merged.PublicCallback;
        document.SystemSettings.MailDelivery = merged.MailDelivery;
        document.SystemSettings.YouTube = merged.YouTube;
        document.SystemSettings.EnableYouTubeSuperChat = merged.EnableYouTubeSuperChat;
        document.SystemSettings.ResultNotificationEmail = merged.ResultNotificationEmail;
        await _repository.SaveUnsafeAsync(document, cancellationToken);
        return new SettingsSectionSnapshot<PaymentSettingsResource>(
            _secrets.RedactPaymentResource(document.SystemSettings),
            ComputeETag());
    }

    public Task<SettingsSectionSnapshot<OvertimeOverlaySettings>> ReadOvertimeAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(document => document.OvertimeOverlay ?? new OvertimeOverlaySettings(), cancellationToken);

    public Task<SettingsSectionSnapshot<OvertimeOverlaySettings>> UpdateOvertimeAsync(OvertimeOverlaySettings value, string? expectedETag, CancellationToken cancellationToken = default) =>
        UpdateAsync((document, _) => document.OvertimeOverlay = value, expectedETag, document => document.OvertimeOverlay ?? new OvertimeOverlaySettings(), cancellationToken);

    private async Task<SettingsSectionSnapshot<T>> ReadAsync<T>(Func<SettingsYamlDocument, T> selector, CancellationToken cancellationToken)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _repository.StoragePath);
        var document = await _repository.ReadUnsafeAsync(cancellationToken);
        return new SettingsSectionSnapshot<T>(selector(document), ComputeETag());
    }

    private async Task<SettingsSectionSnapshot<T>> UpdateAsync<T>(
        Action<SettingsYamlDocument, SettingsYamlDocument> assign,
        string? expectedETag,
        Func<SettingsYamlDocument, T> selector,
        CancellationToken cancellationToken)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _repository.StoragePath);
        var document = await _repository.ReadUnsafeAsync(cancellationToken);
        EnsureETag(expectedETag);
        assign(document, document);
        await _repository.SaveUnsafeAsync(document, cancellationToken);
        return new SettingsSectionSnapshot<T>(selector(document), ComputeETag());
    }

    private void EnsureETag(string? expectedETag)
    {
        if (string.IsNullOrWhiteSpace(expectedETag) || expectedETag == "*") return;
        var actual = ComputeETag();
        if (!string.Equals(expectedETag.Trim(), actual, StringComparison.Ordinal))
            throw new ConfigurationConcurrencyException(expectedETag, actual);
    }

    private string ComputeETag()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(_repository.StoragePath)));
        if (File.Exists(_repository.StoragePath)) hash.AppendData(File.ReadAllBytes(_repository.StoragePath));
        return $"\"{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}\"";
    }
}
