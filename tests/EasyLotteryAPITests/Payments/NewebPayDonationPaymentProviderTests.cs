using System.Security.Cryptography;
using System.Text;
using EasyLotteryApi.Payments;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApiTests.Payments;

[TestClass]
public sealed class NewebPayDonationPaymentProviderTests
{
    private const string MerchantId = "3656445";
    private const string HashKey = "12345678901234567890123456789012";
    private const string HashIv = "1234567890123456";

    [TestMethod]
    public async Task ParseNotificationAsync_ValidJsonResult_AcceptsSuccessfulPayment()
    {
        const string amount = "300";
        const string orderNo = "ORDER_20260723";
        const string tradeNo = "NEWEBPAY123";
        var checkCode = CreateCheckCode(amount, MerchantId, orderNo, tradeNo);
        var result = "{\"MerchantID\":\"3656445\",\"Amt\":\"300\",\"MerchantOrderNo\":\"ORDER_20260723\",\"TradeNo\":\"NEWEBPAY123\",\"PayTime\":\"2026-07-23 12:34:56\",\"CheckCode\":\"" + checkCode + "\"}";
        var provider = new NewebPayDonationPaymentProvider();

        var notification = await provider.ParseNotificationAsync(new PaymentNotificationRequest
        {
            ContentType = "application/x-www-form-urlencoded",
            Body = $"Status=SUCCESS&Result={Uri.EscapeDataString(result)}",
            Headers = new Dictionary<string, string>()
        }, CreateSettings(), CancellationToken.None);

        Assert.IsTrue(notification.SignatureIsValid);
        Assert.IsTrue(notification.IsSuccessful);
        Assert.AreEqual(tradeNo, notification.ExternalId);
        Assert.AreEqual(300m, notification.Amount);
        Assert.AreEqual("匿名贊助者", notification.DisplayName);
        Assert.AreEqual("", notification.Message);
    }

    [TestMethod]
    public async Task ParseNotificationAsync_TamperedAmount_RejectsNotification()
    {
        const string orderNo = "ORDER_20260723";
        const string tradeNo = "NEWEBPAY123";
        var checkCode = CreateCheckCode("300", MerchantId, orderNo, tradeNo);
        var provider = new NewebPayDonationPaymentProvider();

        var notification = await provider.ParseNotificationAsync(new PaymentNotificationRequest
        {
            ContentType = "application/x-www-form-urlencoded",
            Body = $"Status=SUCCESS&MerchantID={MerchantId}&Amt=301&MerchantOrderNo={orderNo}&TradeNo={tradeNo}&CheckCode={checkCode}",
            Headers = new Dictionary<string, string>()
        }, CreateSettings(), CancellationToken.None);

        Assert.IsFalse(notification.SignatureIsValid);
    }

    private static DonationProviderSettings CreateSettings() => new()
    {
        MerchantId = MerchantId,
        ApiKey = HashKey,
        SecretKey = HashIv,
        IsEnabled = true
    };

    private static string CreateCheckCode(string amount, string merchantId, string orderNo, string tradeNo)
    {
        var source = $"HashKey={HashKey}&Amt={amount}&MerchantID={merchantId}&MerchantOrderNo={orderNo}&TradeNo={tradeNo}&HashIV={HashIv}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
}
