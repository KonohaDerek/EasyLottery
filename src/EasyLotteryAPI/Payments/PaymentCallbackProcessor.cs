using System.Text.Json;
using System.Net;
using System.Net.Mail;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
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
    private readonly string _paymentOrdersPath;
    private readonly string _overtimeFeedPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDeserializer _yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
    private readonly ISerializer _yamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
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
        _paymentOrdersPath = Path.Combine(storageDirectory, "easy-lottery-payment-orders.json");
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

            var orders = await ReadJsonAsync<List<RegisteredPaymentOrder>>(_paymentOrdersPath, cancellationToken) ?? [];
            var order = orders.FirstOrDefault(item => string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                                                      string.Equals(item.MerchantOrderNo, notification.MerchantOrderNo, StringComparison.Ordinal));
            if (order is not null)
            {
                order.Status = "paid";
                order.PaidAtUtc = notification.OccurredAtUtc;
                await WriteJsonAsync(_paymentOrdersPath, orders, cancellationToken);
            }

            paymentEvents.Add(new ProcessedPaymentEvent
            {
                ProviderId = providerId,
                ExternalId = notification.ExternalId,
                MerchantOrderNo = notification.MerchantOrderNo,
                Amount = notification.Amount,
                Currency = notification.Currency,
                Status = "paid",
                DisplayName = order?.DisplayName ?? notification.DisplayName,
                Message = order?.Message ?? "",
                PaidAtUtc = notification.OccurredAtUtc,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
            await WriteJsonAsync(_paymentEventsPath, paymentEvents, cancellationToken);

            var donateWins = await ProcessDonateLotteryAsync(notification, cancellationToken);
            var feed = await ReadJsonAsync<List<OvertimeSupportEvent>>(_overtimeFeedPath, cancellationToken) ?? [];
            feed.Add(new OvertimeSupportEvent
            {
                Source = OvertimeSupportSource.ThirdPartyPayment,
                SourceLabel = provider.Descriptor.DisplayName,
                DisplayName = order?.DisplayName ?? notification.DisplayName,
                Message = order?.Message ?? "",
                Amount = notification.Amount,
                AmountDisplay = $"NT${notification.Amount:N0}",
                Currency = notification.Currency,
                OccurredAtUtc = notification.OccurredAtUtc,
                ExternalId = notification.ExternalId
            });
            foreach (var win in donateWins)
            {
                feed.Add(new OvertimeSupportEvent
                {
                    Source = OvertimeSupportSource.ThirdPartyPayment,
                    SourceLabel = "Donate 抽獎中獎",
                    DisplayName = win.DonorName,
                    Message = $"抽中 {win.PrizeName}",
                    Amount = win.Amount,
                    AmountDisplay = $"NT${win.Amount:N0}",
                    Currency = notification.Currency,
                    AvatarUrl = win.PrizeImageUrl,
                    Color = "#ffd166",
                    OccurredAtUtc = win.DrawnAtUtc,
                    ExternalId = $"{win.PaymentExternalId}:{win.Id}"
                });
            }
            await WriteJsonAsync(_overtimeFeedPath, feed, cancellationToken);
            await SendResultNotificationAsync(donateWins, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        await _hub.Clients.All.SendAsync("OvertimeFeedChanged", cancellationToken);
        return PaymentCallbackProcessResult.Success();
    }

    public async Task<RegisteredPaymentOrder> RegisterOrderAsync(PaymentOrderRegistrationRequest request, CancellationToken cancellationToken)
    {
        var providerId = NormalizeProviderId(request.ProviderId);
        if (!_factory.TryGet(providerId, out _) || string.IsNullOrWhiteSpace(request.MerchantOrderNo))
        {
            throw new ArgumentException("請提供支援的金流商與商店訂單編號。");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var orders = await ReadJsonAsync<List<RegisteredPaymentOrder>>(_paymentOrdersPath, cancellationToken) ?? [];
            if (orders.Any(order => string.Equals(order.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) && string.Equals(order.MerchantOrderNo, request.MerchantOrderNo.Trim(), StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("此商店訂單編號已登記。");
            }
            var order = new RegisteredPaymentOrder
            {
                ProviderId = providerId,
                MerchantOrderNo = request.MerchantOrderNo.Trim(),
                DisplayName = request.DisplayName?.Trim() ?? "匿名贊助者",
                Message = request.Message?.Trim() ?? "",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Status = "pending"
            };
            orders.Add(order);
            await WriteJsonAsync(_paymentOrdersPath, orders, cancellationToken);
            return order;
        }
        finally { _gate.Release(); }
    }

    private async Task<IReadOnlyList<DonateLotteryDrawRecord>> ProcessDonateLotteryAsync(PaymentNotification notification, CancellationToken cancellationToken)
    {
        var path = File.Exists(_configPath) ? _configPath : _legacyConfigPath;
        if (!File.Exists(path)) return [];

        var document = _yaml.Deserialize<EasyLotteryConfigDocument>(await File.ReadAllTextAsync(path, cancellationToken)) ?? new();
        var result = DonateLotteryEngine.Process(document, notification.ExternalId, notification.DisplayName, notification.Amount, notification.OccurredAtUtc);
        if (!result.Processed) return [];

        var temporaryPath = $"{_configPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, _yamlSerializer.Serialize(document), cancellationToken);
        File.Move(temporaryPath, _configPath, overwrite: true);
        return result.Wins;
    }

    private async Task SendResultNotificationAsync(IReadOnlyList<DonateLotteryDrawRecord> wins, CancellationToken cancellationToken)
    {
        if (wins.Count == 0) return;
        try
        {
            var path = File.Exists(_configPath) ? _configPath : _legacyConfigPath;
            if (!File.Exists(path)) return;
            var document = _yaml.Deserialize<EasyLotteryConfigDocument>(await File.ReadAllTextAsync(path, cancellationToken));
            var settings = document?.SystemSettings;
            if (settings is null || string.IsNullOrWhiteSpace(settings.ResultNotificationEmail) || !settings.MailDelivery.HasConfiguration) return;
            using var message = new MailMessage(new MailAddress(settings.MailDelivery.FromAddress, settings.MailDelivery.FromName), new MailAddress(settings.ResultNotificationEmail))
            {
                Subject = "EasyLottery Donate 抽獎結果",
                Body = string.Join(Environment.NewLine, wins.Select(win => $"{win.DonorName} 贊助 NT${win.Amount:N0}，抽中：{win.PrizeName}"))
            };
            using var client = new SmtpClient(settings.MailDelivery.SmtpHost, settings.MailDelivery.SmtpPort)
            {
                EnableSsl = settings.MailDelivery.EnableSsl,
                Credentials = new NetworkCredential(settings.MailDelivery.SmtpUsername, settings.MailDelivery.SmtpPassword)
            };
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to send Donate lottery result notification.");
        }
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
    public string MerchantOrderNo { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TWD";
    public string Status { get; set; } = "paid";
    public string DisplayName { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset PaidAtUtc { get; set; }
    public DateTimeOffset ProcessedAtUtc { get; set; }
}

public sealed class PaymentOrderRegistrationRequest
{
    public string ProviderId { get; set; } = "";
    public string MerchantOrderNo { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class RegisteredPaymentOrder
{
    public string ProviderId { get; set; } = "";
    public string MerchantOrderNo { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
}

public sealed record PaymentCallbackProcessResult(bool Accepted, bool IsDuplicate, string Error)
{
    public static PaymentCallbackProcessResult Success() => new(true, false, "");
    public static PaymentCallbackProcessResult Duplicate() => new(true, true, "");
    public static PaymentCallbackProcessResult Rejected(string error) => new(false, false, error);
}
