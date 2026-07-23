using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApi.Payments;

public sealed class EcpayBroadcasterPaymentProvider : IPaymentProvider
{
    public PaymentProviderDescriptor Descriptor { get; } = PaymentProviderCatalog.BuiltIns.Single(provider => provider.Id == PaymentProviderIds.EcpayBroadcaster);

    public Task<PaymentNotification> ParseNotificationAsync(
        PaymentNotificationRequest request,
        DonationProviderSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var envelope = JsonDocument.Parse(request.Body);
            var root = envelope.RootElement;
            var encryptedData = GetString(root, "Data");
            var checkMacValue = GetString(root, "CheckMacValue");
            var merchantId = GetString(root, "MerchantID");
            var data = Decrypt(encryptedData, settings.ApiKey, settings.SecretKey);
            var signatureIsValid = VerifyCheckMac(data, checkMacValue, settings.ApiKey, settings.SecretKey);

            using var payload = JsonDocument.Parse(data);
            var result = payload.RootElement;
            var orderInfo = result.TryGetProperty("OrderInfo", out var order) ? order : result;
            var externalId = GetString(orderInfo, "TradeNo");
            if (string.IsNullOrWhiteSpace(externalId))
            {
                externalId = GetString(orderInfo, "MerchantTradeNo");
            }

            var resultMerchantId = GetString(result, "MerchantID");
            var merchantMatches = string.IsNullOrWhiteSpace(settings.MerchantId) ||
                                  string.Equals(settings.MerchantId, merchantId, StringComparison.Ordinal) ||
                                  string.Equals(settings.MerchantId, resultMerchantId, StringComparison.Ordinal);
            var rtnCode = GetInt(result, "RtnCode");
            var tradeStatus = GetInt(orderInfo, "TradeStatus");

            return Task.FromResult(new PaymentNotification
            {
                ExternalId = externalId,
                SignatureIsValid = signatureIsValid && merchantMatches,
                IsSuccessful = rtnCode == 1 && tradeStatus == 1,
                Amount = GetDecimal(orderInfo, "TradeAmt"),
                DisplayName = GetString(result, "PatronName"),
                Message = GetString(result, "PatronNote"),
                OccurredAtUtc = ParseOccurredAt(GetString(orderInfo, "PaymentDate")),
                FailureReason = signatureIsValid ? (merchantMatches ? "" : "Merchant ID does not match the configured provider.") : "Invalid ECPay CheckMacValue."
            });
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException or ArgumentException)
        {
            return Task.FromResult(new PaymentNotification
            {
                ExternalId = "",
                SignatureIsValid = false,
                IsSuccessful = false,
                FailureReason = "Unable to parse ECPay broadcaster notification."
            });
        }
    }

    internal static bool VerifyCheckMac(string data, string receivedCheckMacValue, string hashKey, string hashIv)
    {
        if (string.IsNullOrWhiteSpace(receivedCheckMacValue))
        {
            return false;
        }

        var encoded = Uri.EscapeDataString($"{hashKey}{data}{hashIv}").ToLowerInvariant();
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(encoded)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(receivedCheckMacValue.Trim().ToUpperInvariant()));
    }

    private static string Decrypt(string encryptedData, string hashKey, string hashIv)
    {
        var cipher = Convert.FromBase64String(encryptedData);
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(hashKey);
        aes.IV = Encoding.UTF8.GetBytes(hashIv);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Uri.UnescapeDataString(Encoding.UTF8.GetString(plaintext));
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.ToString() : "";

    private static int GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static decimal GetDecimal(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number) ? number : 0m;

    private static DateTimeOffset ParseOccurredAt(string value) =>
        DateTimeOffset.TryParse(value, out var occurredAt) ? occurredAt.ToUniversalTime() : DateTimeOffset.UtcNow;
}
