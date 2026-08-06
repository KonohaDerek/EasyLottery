using EasyLotteryDomain.Models.Config;
using MediatR;

namespace EasyLotteryApplication.Settings;

public sealed class SettingsSectionRules
{
    public ObsLayoutSettings Normalize(ObsLayoutSettings? value)
    {
        value ??= new ObsLayoutSettings();
        value.ActiveLayoutKey = RequireKey(value.ActiveLayoutKey, "OBS 版面");
        return value;
    }

    public SoundCueSettings Normalize(SoundCueSettings? value)
    {
        value ??= new SoundCueSettings();
        value.ActivePresetKey = RequireKey(value.ActivePresetKey, "音效");
        return value;
    }

    public VisualStyleSettings Normalize(VisualStyleSettings? value)
    {
        value ??= new VisualStyleSettings();
        value.ActiveThemeKey = RequireKey(value.ActiveThemeKey, "視覺樣式");
        value.BackgroundImageUrl = NormalizeHttpsUrl(value.BackgroundImageUrl, "背景圖片");
        value.BannerImageUrl = NormalizeHttpsUrl(value.BannerImageUrl, "橫幅圖片");
        return value;
    }

    public PaymentSettingsResource Normalize(PaymentSettingsResource? value)
    {
        value ??= new PaymentSettingsResource();
        value.DonationIntegration ??= new DonationIntegrationSettings();
        value.DonationIntegration.Ecpay ??= new DonationProviderSettings { Name = "綠界" };
        value.DonationIntegration.NewebPay ??= new DonationProviderSettings { Name = "藍新" };
        value.DonationIntegration.OenTw ??= new DonationProviderSettings { Name = "oen.tw" };
        value.DonationIntegration.TwitchBits ??= new DonationProviderSettings { Name = "Twitch 小奇點" };
        NormalizeProvider(value.DonationIntegration.Ecpay, "綠界");
        NormalizeProvider(value.DonationIntegration.NewebPay, "藍新");
        NormalizeProvider(value.DonationIntegration.OenTw, "oen.tw");
        NormalizeProvider(value.DonationIntegration.TwitchBits, "Twitch 小奇點");
        value.PublicCallback ??= new PublicCallbackSettings();
        value.MailDelivery ??= new MailDeliverySettings();
        value.YouTube ??= new YouTubeApiSettings();
        value.PublicCallback.CustomDomain = NormalizeHttpsUrl(value.PublicCallback.CustomDomain, "自訂網域");
        value.PublicCallback.ActivePublicBaseUrl = NormalizeHttpsUrl(value.PublicCallback.ActivePublicBaseUrl, "公開網址");
        value.PublicCallback.TunnelProvider = PublicCallbackTunnelProviders.Normalize(value.PublicCallback.TunnelProvider);
        value.MailDelivery.SmtpPort = Math.Clamp(value.MailDelivery.SmtpPort, 0, 65535);
        value.MailDelivery.SmtpHost = (value.MailDelivery.SmtpHost ?? "").Trim();
        value.MailDelivery.SmtpUsername = (value.MailDelivery.SmtpUsername ?? "").Trim();
        value.MailDelivery.FromAddress = (value.MailDelivery.FromAddress ?? "").Trim();
        value.MailDelivery.FromName = string.IsNullOrWhiteSpace(value.MailDelivery.FromName) ? "EasyLottery" : value.MailDelivery.FromName.Trim();
        value.ResultNotificationEmail = (value.ResultNotificationEmail ?? "").Trim();
        return value;
    }

    public OvertimeOverlaySettings Normalize(OvertimeOverlaySettings? value)
    {
        value ??= new OvertimeOverlaySettings();
        value.Title = (value.Title ?? "").Trim();
        value.Subtitle = (value.Subtitle ?? "").Trim();
        value.ThemeKey = RequireKey(value.ThemeKey, "加班台主題");
        value.MessageTemplateKey = RequireKey(value.MessageTemplateKey, "加班台訊息模板");
        value.TextAnimationKey = RequireKey(value.TextAnimationKey, "加班台文字動畫");
        value.MaxVisibleItems = Math.Clamp(value.MaxVisibleItems, 1, 100);
        value.DemoSuperChatAmount = Math.Max(0, value.DemoSuperChatAmount);
        value.DemoEcpayAmount = Math.Max(0, value.DemoEcpayAmount);
        value.SupportMessageVisibleSeconds = Math.Clamp(value.SupportMessageVisibleSeconds, 1, 60);
        value.CompletionFireworksDurationSeconds = Math.Clamp(value.CompletionFireworksDurationSeconds, 2, 15);
        value.SessionState = OvertimeSessionStates.Normalize(value.SessionState);
        value.RewardRules ??= [];
        foreach (var rule in value.RewardRules)
        {
            rule.Label = (rule.Label ?? "").Trim();
            rule.AmountThreshold = Math.Max(0, rule.AmountThreshold);
            rule.AddMinutes = Math.Max(0, rule.AddMinutes);
            rule.AddHours = Math.Max(0, rule.AddHours);
        }
        return value;
    }

    private static void NormalizeProvider(DonationProviderSettings provider, string defaultName)
    {
        provider.Name = string.IsNullOrWhiteSpace(provider.Name) ? defaultName : provider.Name.Trim();
        provider.Environment = PaymentProviderEnvironments.Normalize(provider.Environment);
        provider.Testing ??= new DonationProviderConnectionSettings();
        provider.Production ??= new DonationProviderConnectionSettings();
        provider.MigrateLegacyConfiguration();
        provider.Testing.DonationPageUrl = NormalizeHttpsUrl(provider.Testing.DonationPageUrl, "測試付款頁面");
        provider.Production.DonationPageUrl = NormalizeHttpsUrl(provider.Production.DonationPageUrl, "正式付款頁面");
    }

    private static string RequireKey(string? value, string field)
    {
        var key = value?.Trim();
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException($"{field}設定不可為空。");
        return key;
    }

    private static string NormalizeHttpsUrl(string? value, string field)
    {
        var url = value?.Trim() ?? "";
        if (url.Length == 0) return "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{field}必須使用 HTTPS 網址。");
        return uri.AbsoluteUri.TrimEnd('/');
    }
}

public sealed class GetObsLayoutSettingsQueryHandler(ISettingsSectionRepository repository)
    : IRequestHandler<GetObsLayoutSettingsQuery, SettingsSectionSnapshot<ObsLayoutSettings>>
{
    public Task<SettingsSectionSnapshot<ObsLayoutSettings>> Handle(GetObsLayoutSettingsQuery request, CancellationToken cancellationToken) => repository.ReadObsLayoutAsync(cancellationToken);
}

public sealed class UpdateObsLayoutSettingsCommandHandler(ISettingsSectionRepository repository, SettingsSectionRules rules)
    : IRequestHandler<UpdateObsLayoutSettingsCommand, SettingsSectionSnapshot<ObsLayoutSettings>>
{
    public Task<SettingsSectionSnapshot<ObsLayoutSettings>> Handle(UpdateObsLayoutSettingsCommand request, CancellationToken cancellationToken) => repository.UpdateObsLayoutAsync(rules.Normalize(request.Value), request.ExpectedETag, cancellationToken);
}

public sealed class GetSoundCueSettingsQueryHandler(ISettingsSectionRepository repository)
    : IRequestHandler<GetSoundCueSettingsQuery, SettingsSectionSnapshot<SoundCueSettings>>
{
    public Task<SettingsSectionSnapshot<SoundCueSettings>> Handle(GetSoundCueSettingsQuery request, CancellationToken cancellationToken) => repository.ReadSoundCueAsync(cancellationToken);
}

public sealed class UpdateSoundCueSettingsCommandHandler(ISettingsSectionRepository repository, SettingsSectionRules rules)
    : IRequestHandler<UpdateSoundCueSettingsCommand, SettingsSectionSnapshot<SoundCueSettings>>
{
    public Task<SettingsSectionSnapshot<SoundCueSettings>> Handle(UpdateSoundCueSettingsCommand request, CancellationToken cancellationToken) => repository.UpdateSoundCueAsync(rules.Normalize(request.Value), request.ExpectedETag, cancellationToken);
}

public sealed class GetVisualStyleSettingsQueryHandler(ISettingsSectionRepository repository)
    : IRequestHandler<GetVisualStyleSettingsQuery, SettingsSectionSnapshot<VisualStyleSettings>>
{
    public Task<SettingsSectionSnapshot<VisualStyleSettings>> Handle(GetVisualStyleSettingsQuery request, CancellationToken cancellationToken) => repository.ReadVisualStyleAsync(cancellationToken);
}

public sealed class UpdateVisualStyleSettingsCommandHandler(ISettingsSectionRepository repository, SettingsSectionRules rules)
    : IRequestHandler<UpdateVisualStyleSettingsCommand, SettingsSectionSnapshot<VisualStyleSettings>>
{
    public Task<SettingsSectionSnapshot<VisualStyleSettings>> Handle(UpdateVisualStyleSettingsCommand request, CancellationToken cancellationToken) => repository.UpdateVisualStyleAsync(rules.Normalize(request.Value), request.ExpectedETag, cancellationToken);
}

public sealed class GetPaymentSettingsQueryHandler(ISettingsSectionRepository repository)
    : IRequestHandler<GetPaymentSettingsQuery, SettingsSectionSnapshot<PaymentSettingsResource>>
{
    public Task<SettingsSectionSnapshot<PaymentSettingsResource>> Handle(GetPaymentSettingsQuery request, CancellationToken cancellationToken) => repository.ReadPaymentsAsync(cancellationToken);
}

public sealed class UpdatePaymentSettingsCommandHandler(ISettingsSectionRepository repository, SettingsSectionRules rules)
    : IRequestHandler<UpdatePaymentSettingsCommand, SettingsSectionSnapshot<PaymentSettingsResource>>
{
    public Task<SettingsSectionSnapshot<PaymentSettingsResource>> Handle(UpdatePaymentSettingsCommand request, CancellationToken cancellationToken) => repository.UpdatePaymentsAsync(rules.Normalize(request.Value), request.ExpectedETag, cancellationToken);
}

public sealed class GetOvertimeSettingsQueryHandler(ISettingsSectionRepository repository)
    : IRequestHandler<GetOvertimeSettingsQuery, SettingsSectionSnapshot<OvertimeOverlaySettings>>
{
    public Task<SettingsSectionSnapshot<OvertimeOverlaySettings>> Handle(GetOvertimeSettingsQuery request, CancellationToken cancellationToken) => repository.ReadOvertimeAsync(cancellationToken);
}

public sealed class UpdateOvertimeSettingsCommandHandler(ISettingsSectionRepository repository, SettingsSectionRules rules)
    : IRequestHandler<UpdateOvertimeSettingsCommand, SettingsSectionSnapshot<OvertimeOverlaySettings>>
{
    public Task<SettingsSectionSnapshot<OvertimeOverlaySettings>> Handle(UpdateOvertimeSettingsCommand request, CancellationToken cancellationToken) => repository.UpdateOvertimeAsync(rules.Normalize(request.Value), request.ExpectedETag, cancellationToken);
}
