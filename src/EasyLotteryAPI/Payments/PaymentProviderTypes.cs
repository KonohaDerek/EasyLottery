using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApi.Payments;

public static class PaymentProviderAliases
{
    private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ecpay"] = PaymentProviderIds.EcpayBroadcaster,
        ["newebpay"] = PaymentProviderIds.NewebPayDonation
    };

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? "";
        return Values.TryGetValue(normalized, out var providerId) ? providerId : normalized;
    }
}
