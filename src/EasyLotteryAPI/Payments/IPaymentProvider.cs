using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApi.Payments;

/// <summary>
/// Server-side extension point for a payment provider package. Implementations must validate
/// the provider signature before returning a successful notification.
/// </summary>
public interface IPaymentProvider
{
    PaymentProviderDescriptor Descriptor { get; }

    Task<PaymentNotification> ParseNotificationAsync(
        PaymentNotificationRequest request,
        DonationProviderSettings settings,
        CancellationToken cancellationToken);
}

public sealed class PaymentNotificationRequest
{
    public required string ContentType { get; init; }
    public required string Body { get; init; }
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}

public sealed class PaymentNotification
{
    public required string ExternalId { get; init; }
    public string MerchantOrderNo { get; init; } = "";
    public required bool IsSuccessful { get; init; }
    public required bool SignatureIsValid { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TWD";
    public string DisplayName { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string FailureReason { get; init; } = "";
}
