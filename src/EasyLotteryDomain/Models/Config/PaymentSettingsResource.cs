namespace EasyLotteryDomain.Models.Config;

/// <summary>
/// The payment configuration boundary exposed by the REST API. It groups only
/// payment, callback and notification settings; unrelated system settings remain
/// outside this resource and cannot be overwritten by a payment update.
/// </summary>
public sealed class PaymentSettingsResource
{
    public DonationIntegrationSettings DonationIntegration { get; set; } = new();
    public PublicCallbackSettings PublicCallback { get; set; } = new();
    public MailDeliverySettings MailDelivery { get; set; } = new();
    public YouTubeApiSettings YouTube { get; set; } = new();
    public bool EnableYouTubeSuperChat { get; set; }
    public string ResultNotificationEmail { get; set; } = "";
}
