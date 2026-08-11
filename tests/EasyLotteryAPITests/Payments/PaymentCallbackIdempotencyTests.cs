using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using EasyLotteryApplication.Payments;
using EasyLotteryApi;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests.Payments;

[TestClass]
public sealed class PaymentCallbackIdempotencyTests
{
    private const string MerchantId = "3656445";
    private const string HashKey = "12345678901234567890123456789012";
    private const string HashIv = "1234567890123456";

    [TestMethod]
    public async Task Notify_ConcurrentDuplicateCallbacks_ProducesOneProcessedEvent()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = LoginAsync(factory);
        await SaveSettingsAsync(client, adminToken);

        using var first = CreateNotifyRequest();
        using var second = CreateNotifyRequest();
        var responses = await Task.WhenAll(client.SendAsync(first), client.SendAsync(second));

        Assert.IsTrue(responses.All(response => response.StatusCode == HttpStatusCode.OK));
        foreach (var response in responses) response.Dispose();

        using var eventsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/payments/events");
        eventsRequest.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var eventsResponse = await client.SendAsync(eventsRequest);
        eventsResponse.EnsureSuccessStatusCode();
        var events = await eventsResponse.Content.ReadFromJsonAsync<List<ProcessedPaymentEvent>>();

        Assert.IsNotNull(events);
        Assert.AreEqual(1, events!.Count);
        Assert.AreEqual("completed", events[0].ProcessingState);
        Assert.AreEqual(1, events[0].ProcessingAttempt);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Directory"] = Path.Combine(Path.GetTempPath(), $"easy-lottery-payment-api-tests-{Guid.NewGuid():N}")
            })));

    private static string LoginAsync(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;

    private static async Task SaveSettingsAsync(HttpClient client, string adminToken)
    {
        var document = new EasyLotteryConfigDocument();
        document.SystemSettings.DonationIntegration.NewebPay.Testing = new DonationProviderConnectionSettings
        {
            IsEnabled = true,
            MerchantId = MerchantId,
            ApiKey = HashKey,
            SecretKey = HashIv
        };
        var yaml = YamlSerialization.CreateSerializerBuilder().Build().Serialize(document);
        var content = new StringContent(yaml);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/yaml");
        using var request = new HttpRequestMessage(HttpMethod.Put, "/settings") { Content = content };
        request.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateNotifyRequest()
    {
        const string orderNo = "ORDER_IDEMPOTENCY";
        const string tradeNo = "TRADE_IDEMPOTENCY";
        const string amount = "100";
        var checkCode = CreateCheckCode(amount, orderNo, tradeNo);
        var result = $"{{\"MerchantID\":\"{MerchantId}\",\"Amt\":\"{amount}\",\"MerchantOrderNo\":\"{orderNo}\",\"TradeNo\":\"{tradeNo}\",\"PayTime\":\"2026-08-06 12:34:56\",\"CheckCode\":\"{checkCode}\"}}";
        var body = $"Status=SUCCESS&Result={Uri.EscapeDataString(result)}";
        var content = new StringContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        return new HttpRequestMessage(HttpMethod.Post, "/api/payments/newebpay/notify") { Content = content };
    }

    private static string CreateCheckCode(string amount, string orderNo, string tradeNo)
    {
        var source = $"HashKey={HashKey}&Amt={amount}&MerchantID={MerchantId}&MerchantOrderNo={orderNo}&TradeNo={tradeNo}&HashIV={HashIv}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private sealed record TokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
}
