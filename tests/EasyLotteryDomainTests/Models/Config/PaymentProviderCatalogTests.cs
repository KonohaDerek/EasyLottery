using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomainTests.Models.Config;

[TestClass]
public sealed class PaymentProviderCatalogTests
{
    [TestMethod]
    public void BuiltIns_ExposeEachSupportedProviderWithUniqueCallbackPath()
    {
        var providers = PaymentProviderCatalog.BuiltIns;

        CollectionAssert.AreEquivalent(
            new[] { PaymentProviderIds.EcpayBroadcaster, PaymentProviderIds.NewebPayDonation, PaymentProviderIds.Oen },
            providers.Select(provider => provider.Id).ToArray());
        Assert.AreEqual(providers.Count, providers.Select(provider => provider.CallbackPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.IsTrue(providers.All(provider => provider.MarketPackageId.StartsWith("payment.", StringComparison.Ordinal)));
    }

    [DataTestMethod]
    [DataRow(null, PaymentProviderEnvironments.Testing)]
    [DataRow("testing", PaymentProviderEnvironments.Testing)]
    [DataRow("PRODUCTION", PaymentProviderEnvironments.Production)]
    [DataRow("unexpected", PaymentProviderEnvironments.Testing)]
    public void EnvironmentNormalization_OnlyAcceptsTestingAndProduction(string? input, string expected)
    {
        Assert.AreEqual(expected, PaymentProviderEnvironments.Normalize(input));
    }
}
