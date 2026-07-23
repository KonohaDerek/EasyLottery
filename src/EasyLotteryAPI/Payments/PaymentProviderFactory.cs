using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApi.Payments;

/// <summary>
/// Resolves built-in and future reviewed Market provider packages by a stable provider id.
/// Arbitrary downloaded code is intentionally not loaded at runtime.
/// </summary>
public sealed class PaymentProviderFactory
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;

    public PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers.ToDictionary(provider => provider.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string providerId, out IPaymentProvider? provider) =>
        _providers.TryGetValue(NormalizeProviderId(providerId), out provider);

    public IReadOnlyCollection<PaymentProviderDescriptor> AvailableProviders =>
        _providers.Values.Select(provider => provider.Descriptor).ToArray();

    private static string NormalizeProviderId(string providerId) => providerId.Trim().ToLowerInvariant() switch
    {
        "ecpay" => PaymentProviderIds.EcpayBroadcaster,
        "newebpay" => PaymentProviderIds.NewebPayDonation,
        _ => providerId
    };
}
