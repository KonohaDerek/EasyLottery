using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomainTests.Models.Config;

[TestClass]
public sealed class DonationProviderSettingsTests
{
    [TestMethod]
    public void ActiveConnection_KeepsTestingAndProductionParametersSeparate()
    {
        var provider = new DonationProviderSettings
        {
            Environment = PaymentProviderEnvironments.Testing,
            Testing = new DonationProviderConnectionSettings { MerchantId = "test-merchant", ApiKey = "test-key", DonationPageUrl = "https://test.example/donate" }
        };

        provider.Environment = PaymentProviderEnvironments.Production;
        provider.Production = new DonationProviderConnectionSettings { MerchantId = "production-merchant", ApiKey = "production-key", DonationPageUrl = "https://production.example/donate" };

        provider.Environment = PaymentProviderEnvironments.Testing;

        Assert.AreEqual("test-merchant", provider.GetActiveConnection().MerchantId);
        Assert.AreEqual("test-key", provider.GetActiveConnection().ApiKey);
        Assert.AreEqual("https://test.example/donate", provider.GetActiveConnection().DonationPageUrl);
        Assert.AreEqual("production-merchant", provider.Production.MerchantId);
        Assert.AreEqual("production-key", provider.Production.ApiKey);
        Assert.AreNotSame(provider.Testing, provider.Production);
    }
}
