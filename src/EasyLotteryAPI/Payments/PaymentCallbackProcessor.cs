using System.Net;
using System.Net.Mail;
using EasyLotteryApplication.Payments;
using EasyLotteryApplication.Settings;
using EasyLotteryApi;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Overtime;
using EasyLotteryDomain.Services;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi.Payments;

public sealed class PaymentCallbackProcessor
{
    private readonly PaymentProviderFactory _factory;
    private readonly IHubContext<OvertimeHub> _hub;
    private readonly ILogger<PaymentCallbackProcessor> _logger;
    private readonly IEasyLotteryConfigRepository _settingsStore;
    private readonly IPaymentEventRepository _paymentEventRepository;
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IOvertimeFeedRepository _overtimeFeedRepository;

    public PaymentCallbackProcessor(
        PaymentProviderFactory factory,
        IHubContext<OvertimeHub> hub,
        ILogger<PaymentCallbackProcessor> logger,
        IEasyLotteryConfigRepository settingsStore,
        IPaymentEventRepository paymentEventRepository,
        IPaymentOrderRepository paymentOrderRepository,
        IOvertimeFeedRepository overtimeFeedRepository)
    {
        _factory = factory;
        _hub = hub;
        _logger = logger;
        _settingsStore = settingsStore;
        _paymentEventRepository = paymentEventRepository;
        _paymentOrderRepository = paymentOrderRepository;
        _overtimeFeedRepository = overtimeFeedRepository;
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

        var paymentEvents = await _paymentEventRepository.ListAsync(cancellationToken);
        if (paymentEvents.Any(item => string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                                      string.Equals(item.ExternalId, notification.ExternalId, StringComparison.Ordinal)))
        {
            return PaymentCallbackProcessResult.Duplicate();
        }

        var orders = (await _paymentOrderRepository.ListAsync(cancellationToken)).ToList();
        var order = orders.FirstOrDefault(item => string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals(item.MerchantOrderNo, notification.MerchantOrderNo, StringComparison.Ordinal));
        if (order is not null)
        {
            order.Status = "paid";
            order.PaidAtUtc = notification.OccurredAtUtc;
            await _paymentOrderRepository.SaveAsync(orders, cancellationToken);
        }

        await _paymentEventRepository.AppendAsync(new ProcessedPaymentEvent
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
        }, cancellationToken);

        var donateWins = await ProcessDonateLotteryAsync(notification, provider.Descriptor.DisplayName, cancellationToken);
        var feed = (await _overtimeFeedRepository.ListAsync(cancellationToken)).ToList();
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
        await _overtimeFeedRepository.SaveAsync(feed, cancellationToken);
        await SendResultNotificationAsync(donateWins, cancellationToken);

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

        var orders = (await _paymentOrderRepository.ListAsync(cancellationToken)).ToList();
        if (orders.Any(order => string.Equals(order.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(order.MerchantOrderNo, request.MerchantOrderNo.Trim(), StringComparison.Ordinal)))
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
        await _paymentOrderRepository.SaveAsync(orders, cancellationToken);
        return order;
    }

    public async Task<IReadOnlyList<ProcessedPaymentEvent>> ListProcessedEventsAsync(CancellationToken cancellationToken)
    {
        return (await _paymentEventRepository.ListAsync(cancellationToken))
            .OrderByDescending(item => item.PaidAtUtc)
            .ToList();
    }

    private async Task<IReadOnlyList<DonateLotteryDrawRecord>> ProcessDonateLotteryAsync(PaymentNotification notification, string paymentMethod, CancellationToken cancellationToken)
    {
        var result = await _settingsStore.UpdateAsync(document => DonateLotteryEngine.Process(document, notification.ExternalId, notification.DisplayName, notification.Amount, notification.OccurredAtUtc, donationMessage: notification.Message, paymentMethod: paymentMethod), cancellationToken);
        return result.Processed ? result.Wins : [];
    }

    private async Task SendResultNotificationAsync(IReadOnlyList<DonateLotteryDrawRecord> wins, CancellationToken cancellationToken)
    {
        if (wins.Count == 0) return;
        try
        {
            var document = await _settingsStore.ReadAsync(cancellationToken);
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
        var document = await _settingsStore.ReadAsync(cancellationToken);
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
}
