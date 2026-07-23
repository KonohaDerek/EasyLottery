using System.Text.Json;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Overtime;
using Microsoft.AspNetCore.SignalR;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EasyLotteryApi.Payments;

public sealed class PaymentCallbackProcessor
{
    private readonly PaymentProviderFactory _factory;
    private readonly IHubContext<OvertimeHub> _hub;
    private readonly ILogger<PaymentCallbackProcessor> _logger;
    private readonly string _configPath;
    private readonly string _legacyConfigPath;
    private readonly string _paymentEventsPath;
    private readonly string _overtimeFeedPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDeserializer _yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public PaymentCallbackProcessor(
        PaymentProviderFactory factory,
        IHubContext<OvertimeHub> hub,
        ILogger<PaymentCallbackProcessor> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _factory = factory;
        _hub = hub;
        _logger = logger;
        var storageDirectory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(storageDirectory);
        _configPath = Path.Combine(storageDirectory, "settings.yaml");
        _legacyConfigPath = Path.Combine(storageDirectory, "easy-lottery.yaml");
        _paymentEventsPath = Path.Combine(storageDirectory, "easy-lottery-payment-events.json");
        _overtimeFeedPath = Path.Combine(storageDirectory, "easy-lottery-overtime-feed.json");
    }

    public async Task<PaymentCallbackProcessResult> ProcessAsync(string providerId, PaymentNotificationRequest request, CancellationToken cancellationToken)
    {
        providerId = NormalizeProviderId(providerId);
        if (!_factory.TryGet(providerId, out var provider) || provider is null)
        {
            return PaymentCallbackProcessResult.Rejected("Unsupported payment provider.");
        }

        var settings = await LoadSettingsAsync(providerId, cancellationToken);
        if (settings is null || !settings.IsEnabled)
        {
            return PaymentCallbackProcessResult.Rejected("Payment provider is disabled.");
        }

        var notification = await provider.ParseNotificationAsync(request, settings, cancellationToken);
        if (!notification.SignatureIsValid || !notification.IsSuccessful || string.IsNullOrWhiteSpace(notification.ExternalId))
        {
            _logger.LogWarning("Rejected payment callback for provider {Provider}: {Reason}", providerId, notification.FailureReason);
            return PaymentCallbackProcessResult.Rejected(notification.FailureReason);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var paymentEvents = await ReadJsonAsync<List<ProcessedPaymentEvent>>(_paymentEventsPath, cancellationToken) ?? [];
            if (paymentEvents.Any(item => string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                                          string.Equals(item.ExternalId, notification.ExternalId, StringComparison.Ordinal)))
            {
                return PaymentCallbackProcessResult.Duplicate();
            }

            paymentEvents.Add(new ProcessedPaymentEvent
            {
                ProviderId = providerId,
                ExternalId = notification.ExternalId,
                Amount = notification.Amount,
                Currency = notification.Currency,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
            await WriteJsonAsync(_paymentEventsPath, paymentEvents, cancellationToken);

            var feed = await ReadJsonAsync<List<OvertimeSupportEvent>>(_overtimeFeedPath, cancellationToken) ?? [];
            feed.Add(new OvertimeSupportEvent
            {
                Source = OvertimeSupportSource.ThirdPartyPayment,
                SourceLabel = provider.Descriptor.DisplayName,
                DisplayName = notification.DisplayName,
                Message = notification.Message,
                Amount = notification.Amount,
                AmountDisplay = $"NT${notification.Amount:N0}",
                Currency = notification.Currency,
                OccurredAtUtc = notification.OccurredAtUtc,
                ExternalId = notification.ExternalId
            });
            await WriteJsonAsync(_overtimeFeedPath, feed, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        await _hub.Clients.All.SendAsync("OvertimeFeedChanged", cancellationToken);
        return PaymentCallbackProcessResult.Success();
    }

    private async Task<DonationProviderSettings?> LoadSettingsAsync(string providerId, CancellationToken cancellationToken)
    {
        var configPath = File.Exists(_configPath) ? _configPath : _legacyConfigPath;
        if (!File.Exists(configPath))
        {
            return null;
        }

        var document = _yaml.Deserialize<EasyLotteryConfigDocument>(await File.ReadAllTextAsync(configPath, cancellationToken));
        var providers = document?.SystemSettings?.DonationIntegration;
        var provider = providerId switch
        {
            PaymentProviderIds.EcpayBroadcaster => providers?.Ecpay,
            PaymentProviderIds.NewebPayDonation => providers?.NewebPay,
            PaymentProviderIds.Oen => providers?.OenTw,
            _ => null
        };
        provider?.MigrateLegacyConfiguration();
        provider?.ApplyActiveConnection();
        return provider;
    }

    private static string NormalizeProviderId(string providerId) => providerId.Trim().ToLowerInvariant() switch
    {
        "ecpay" => PaymentProviderIds.EcpayBroadcaster,
        "newebpay" => PaymentProviderIds.NewebPayDonation,
        _ => providerId
    };

    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, cancellationToken: cancellationToken);
        }
        File.Move(temporaryPath, path, overwrite: true);
    }
}

public sealed class ProcessedPaymentEvent
{
    public string ProviderId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TWD";
    public DateTimeOffset ProcessedAtUtc { get; set; }
}

public sealed record PaymentCallbackProcessResult(bool Accepted, bool IsDuplicate, string Error)
{
    public static PaymentCallbackProcessResult Success() => new(true, false, "");
    public static PaymentCallbackProcessResult Duplicate() => new(true, true, "");
    public static PaymentCallbackProcessResult Rejected(string error) => new(false, false, error);
}
