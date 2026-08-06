using EasyLotteryDomain.Models.Config;
using MediatR;

namespace EasyLotteryApplication.Settings;

public sealed record SettingsSectionSnapshot<T>(T Value, string ETag);

public interface ISettingsSectionRepository
{
    Task<SettingsSectionSnapshot<ObsLayoutSettings>> ReadObsLayoutAsync(CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<ObsLayoutSettings>> UpdateObsLayoutAsync(ObsLayoutSettings value, string? expectedETag, CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<SoundCueSettings>> ReadSoundCueAsync(CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<SoundCueSettings>> UpdateSoundCueAsync(SoundCueSettings value, string? expectedETag, CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<VisualStyleSettings>> ReadVisualStyleAsync(CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<VisualStyleSettings>> UpdateVisualStyleAsync(VisualStyleSettings value, string? expectedETag, CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<PaymentSettingsResource>> ReadPaymentsAsync(CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<PaymentSettingsResource>> UpdatePaymentsAsync(PaymentSettingsResource value, string? expectedETag, CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<OvertimeOverlaySettings>> ReadOvertimeAsync(CancellationToken cancellationToken = default);
    Task<SettingsSectionSnapshot<OvertimeOverlaySettings>> UpdateOvertimeAsync(OvertimeOverlaySettings value, string? expectedETag, CancellationToken cancellationToken = default);
}

public sealed record GetObsLayoutSettingsQuery : IRequest<SettingsSectionSnapshot<ObsLayoutSettings>>;
public sealed record UpdateObsLayoutSettingsCommand(ObsLayoutSettings Value, string? ExpectedETag) : IRequest<SettingsSectionSnapshot<ObsLayoutSettings>>;
public sealed record GetSoundCueSettingsQuery : IRequest<SettingsSectionSnapshot<SoundCueSettings>>;
public sealed record UpdateSoundCueSettingsCommand(SoundCueSettings Value, string? ExpectedETag) : IRequest<SettingsSectionSnapshot<SoundCueSettings>>;
public sealed record GetVisualStyleSettingsQuery : IRequest<SettingsSectionSnapshot<VisualStyleSettings>>;
public sealed record UpdateVisualStyleSettingsCommand(VisualStyleSettings Value, string? ExpectedETag) : IRequest<SettingsSectionSnapshot<VisualStyleSettings>>;
public sealed record GetPaymentSettingsQuery : IRequest<SettingsSectionSnapshot<PaymentSettingsResource>>;
public sealed record UpdatePaymentSettingsCommand(PaymentSettingsResource Value, string? ExpectedETag) : IRequest<SettingsSectionSnapshot<PaymentSettingsResource>>;
public sealed record GetOvertimeSettingsQuery : IRequest<SettingsSectionSnapshot<OvertimeOverlaySettings>>;
public sealed record UpdateOvertimeSettingsCommand(OvertimeOverlaySettings Value, string? ExpectedETag) : IRequest<SettingsSectionSnapshot<OvertimeOverlaySettings>>;
