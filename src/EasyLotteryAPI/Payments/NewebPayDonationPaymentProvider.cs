using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyLotteryDomain.Models.Config;
using Microsoft.AspNetCore.WebUtilities;

namespace EasyLotteryApi.Payments;

/// <summary>
/// Validates the Form POST notification from NewebPay's donation platform.
/// The callback CheckCode is a SHA-256 hash of the four immutable payment
/// identifiers, wrapped by the merchant HashKey and HashIV.
/// </summary>
public sealed class NewebPayDonationPaymentProvider : IPaymentProvider
{
    public PaymentProviderDescriptor Descriptor { get; } = PaymentProviderCatalog.BuiltIns.Single(provider => provider.Id == PaymentProviderIds.NewebPayDonation);

    public Task<PaymentNotification> ParseNotificationAsync(
        PaymentNotificationRequest request,
        DonationProviderSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var values = QueryHelpers.ParseQuery(request.Body)
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            var payload = ReadResult(values);

            var merchantId = GetValue(payload, "MerchantID");
            var merchantOrderNo = GetValue(payload, "MerchantOrderNo");
            var tradeNo = GetValue(payload, "TradeNo");
            var amount = ParseAmount(GetValue(payload, "Amt"));
            var checkCode = GetValue(payload, "CheckCode");
            var signatureIsValid = VerifyCheckCode(
                amount,
                merchantId,
                merchantOrderNo,
                tradeNo,
                checkCode,
                settings.ApiKey,
                settings.SecretKey);
            var merchantMatches = !string.IsNullOrWhiteSpace(settings.MerchantId) &&
                                  string.Equals(settings.MerchantId, merchantId, StringComparison.Ordinal);
            var isSuccessful = string.Equals(GetValue(values, "Status"), "SUCCESS", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new PaymentNotification
            {
                ExternalId = tradeNo,
                MerchantOrderNo = merchantOrderNo,
                SignatureIsValid = signatureIsValid && merchantMatches,
                IsSuccessful = isSuccessful,
                Amount = amount,
                DisplayName = "匿名贊助者",
                // The documented NotifyURL payload does not include a donor message.
                // Do not use the payment-status Message as an audience-facing comment.
                Message = "",
                OccurredAtUtc = ParseOccurredAt(GetValue(payload, "PayTime")),
                FailureReason = !signatureIsValid
                    ? "Invalid NewebPay CheckCode."
                    : !merchantMatches
                        ? "Merchant ID does not match the configured provider."
                        : isSuccessful
                            ? ""
                            : "NewebPay payment was not successful."
            });
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        {
            return Task.FromResult(new PaymentNotification
            {
                ExternalId = "",
                SignatureIsValid = false,
                IsSuccessful = false,
                FailureReason = "Unable to parse NewebPay donation notification."
            });
        }
    }

    internal static bool VerifyCheckCode(
        decimal amount,
        string merchantId,
        string merchantOrderNo,
        string tradeNo,
        string receivedCheckCode,
        string hashKey,
        string hashIv)
    {
        if (string.IsNullOrWhiteSpace(receivedCheckCode) ||
            string.IsNullOrWhiteSpace(hashKey) ||
            string.IsNullOrWhiteSpace(hashIv))
        {
            return false;
        }

        var amountText = amount.ToString("0.############################", CultureInfo.InvariantCulture);
        var source = $"HashKey={hashKey}&Amt={amountText}&MerchantID={merchantId}&MerchantOrderNo={merchantOrderNo}&TradeNo={tradeNo}&HashIV={hashIv}";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(receivedCheckCode.Trim().ToUpperInvariant()));
    }

    private static IReadOnlyDictionary<string, string> ReadResult(IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue("Result", out var result) || string.IsNullOrWhiteSpace(result))
        {
            return values;
        }

        using var document = JsonDocument.Parse(result);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("NewebPay Result must be a JSON object.");
        }

        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private static string GetValue(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) ? value.Trim() : "";

    private static decimal ParseAmount(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;

    private static DateTimeOffset ParseOccurredAt(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var occurredAt)
            ? occurredAt.ToUniversalTime()
            : DateTimeOffset.UtcNow;
}
