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
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);

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

        var orders = (await _paymentOrderRepository.ListAsync(cancellationToken)).ToList();
        var order = orders.FirstOrDefault(item => string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals(item.MerchantOrderNo, notification.MerchantOrderNo, StringComparison.Ordinal));

        var paymentEvent = new ProcessedPaymentEvent
        {
            ProviderId = providerId,
            ExternalId = notification.ExternalId,
            MerchantOrderNo = notification.MerchantOrderNo,
            Amount = notification.Amount,
            Currency = notification.Currency,
            Status = "paid",
            DisplayName = order?.DisplayName ?? notification.DisplayName,
            Message = order?.Message ?? notification.Message,
            PaidAtUtc = notification.OccurredAtUtc,
            ProcessedAtUtc = DateTimeOffset.UtcNow
        };

        var claim = await TryClaimPaymentEventAsync(paymentEvent, cancellationToken);
        if (!claim.IsClaimed)
        {
            return PaymentCallbackProcessResult.Duplicate();
        }

        paymentEvent = claim.Event;
        try
        {
            if (!paymentEvent.OrderApplied)
            {
                if (order is not null)
                {
                    if (_paymentOrderRepository is IPaymentOrderMutationRepository mutationRepository)
                    {
                        await mutationRepository.MutateAsync(currentOrders =>
                        {
                            var currentOrder = currentOrders.FirstOrDefault(item =>
                                string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(item.MerchantOrderNo, notification.MerchantOrderNo, StringComparison.Ordinal));
                            if (currentOrder is not null)
                            {
                                currentOrder.Status = "paid";
                                currentOrder.PaidAtUtc = notification.OccurredAtUtc;
                            }

                            return Task.CompletedTask;
                        }, cancellationToken);
                    }
                    else
                    {
                        order.Status = "paid";
                        order.PaidAtUtc = notification.OccurredAtUtc;
                        await _paymentOrderRepository.SaveAsync(orders, cancellationToken);
                    }
                }

                paymentEvent.OrderApplied = true;
                await PersistPaymentEventAsync(paymentEvent, cancellationToken);
            }

            IReadOnlyList<DonateLotteryDrawRecord> donateWins;
            if (!paymentEvent.DonateApplied)
            {
                var result = await ProcessDonateLotteryAsync(notification, provider.Descriptor.DisplayName, cancellationToken);
                donateWins = result.Processed
                    ? result.Wins
                    : await ReadDonateWinsAsync(notification.ExternalId, cancellationToken);
                paymentEvent.DonateApplied = true;
                await PersistPaymentEventAsync(paymentEvent, cancellationToken);
            }
            else
            {
                donateWins = await ReadDonateWinsAsync(notification.ExternalId, cancellationToken);
            }

            if (!paymentEvent.OvertimeFeedApplied)
            {
                await AppendOvertimeFeedAsync(notification, provider.Descriptor.DisplayName, order, donateWins, cancellationToken);
                paymentEvent.OvertimeFeedApplied = true;
                await PersistPaymentEventAsync(paymentEvent, cancellationToken);
            }

            if (!paymentEvent.ResultNotificationSent)
            {
                if (!await SendResultNotificationAsync(donateWins, cancellationToken))
                {
                    throw new InvalidOperationException("抽獎結果通知尚未成功寄送。");
                }

                paymentEvent.ResultNotificationSent = true;
                await PersistPaymentEventAsync(paymentEvent, cancellationToken);
            }

            paymentEvent.ProcessingState = "completed";
            paymentEvent.ProcessingCompletedAtUtc = DateTimeOffset.UtcNow;
            paymentEvent.ProcessingFailureReason = "";
            paymentEvent.ProcessedAtUtc = DateTimeOffset.UtcNow;
            await PersistPaymentEventAsync(paymentEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Payment callback processing failed for {Provider} / {ExternalId}.", providerId, notification.ExternalId);
            await MarkPaymentEventFailedAsync(paymentEvent, exception, CancellationToken.None);
            return PaymentCallbackProcessResult.Rejected("付款事件處理失敗，請稍後重試。");
        }

        try
        {
            await _hub.Clients.All.SendAsync("OvertimeFeedChanged", cancellationToken);
        }
        catch (Exception exception)
        {
            // The payment event is already durably completed. A disconnected OBS
            // client must not cause the payment provider to retry the callback.
            _logger.LogWarning(exception, "Unable to broadcast overtime feed update after payment {ExternalId}.", paymentEvent.ExternalId);
        }

        return PaymentCallbackProcessResult.Success();
    }

    public async Task<RegisteredPaymentOrder> RegisterOrderAsync(PaymentOrderRegistrationRequest request, CancellationToken cancellationToken)
    {
        var providerId = NormalizeProviderId(request.ProviderId);
        if (!_factory.TryGet(providerId, out _) || string.IsNullOrWhiteSpace(request.MerchantOrderNo))
        {
            throw new ArgumentException("請提供支援的金流商與商店訂單編號。");
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

        if (_paymentOrderRepository is IPaymentOrderMutationRepository mutationRepository)
        {
            await mutationRepository.MutateAsync(orders =>
            {
                if (orders.Any(existing => string.Equals(existing.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                                           string.Equals(existing.MerchantOrderNo, order.MerchantOrderNo, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("此商店訂單編號已登記。");
                }

                orders.Add(order);
                return Task.CompletedTask;
            }, cancellationToken);
        }
        else
        {
            var orders = (await _paymentOrderRepository.ListAsync(cancellationToken)).ToList();
            if (orders.Any(existing => string.Equals(existing.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                                       string.Equals(existing.MerchantOrderNo, order.MerchantOrderNo, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("此商店訂單編號已登記。");
            }

            orders.Add(order);
            await _paymentOrderRepository.SaveAsync(orders, cancellationToken);
        }

        return order;
    }

    public async Task<IReadOnlyList<ProcessedPaymentEvent>> ListProcessedEventsAsync(CancellationToken cancellationToken)
    {
        return (await _paymentEventRepository.ListAsync(cancellationToken))
            .OrderByDescending(item => item.PaidAtUtc)
            .ToList();
    }

    private async Task<DonateLotteryProcessResult> ProcessDonateLotteryAsync(PaymentNotification notification, string paymentMethod, CancellationToken cancellationToken)
    {
        return await _settingsStore.UpdateAsync(document => DonateLotteryEngine.Process(document, notification.ExternalId, notification.DisplayName, notification.Amount, notification.OccurredAtUtc, donationMessage: notification.Message, paymentMethod: paymentMethod), cancellationToken);
    }

    private async Task<IReadOnlyList<DonateLotteryDrawRecord>> ReadDonateWinsAsync(string externalId, CancellationToken cancellationToken)
    {
        var document = await _settingsStore.ReadAsync(cancellationToken);
        return document.DonateLotteryDrawRecords
            .Where(record => string.Equals(record.PaymentExternalId, externalId, StringComparison.Ordinal) && record.IsWinning)
            .ToList();
    }

    private async Task AppendOvertimeFeedAsync(
        PaymentNotification notification,
        string paymentMethod,
        RegisteredPaymentOrder? order,
        IReadOnlyList<DonateLotteryDrawRecord> donateWins,
        CancellationToken cancellationToken)
    {
        var displayName = order?.DisplayName ?? notification.DisplayName;
        var message = order?.Message ?? notification.Message;
        Task MutateAsync(List<OvertimeSupportEvent> feed)
        {
            if (!feed.Any(item => string.Equals(item.ExternalId, notification.ExternalId, StringComparison.Ordinal)))
            {
                feed.Add(new OvertimeSupportEvent
                {
                    Source = OvertimeSupportSource.ThirdPartyPayment,
                    SourceLabel = paymentMethod,
                    DisplayName = displayName,
                    Message = message,
                    Amount = notification.Amount,
                    AmountDisplay = $"NT${notification.Amount:N0}",
                    Currency = notification.Currency,
                    OccurredAtUtc = notification.OccurredAtUtc,
                    ExternalId = notification.ExternalId
                });
            }

            foreach (var win in donateWins)
            {
                var externalId = $"{win.PaymentExternalId}:{win.Id}";
                if (feed.Any(item => string.Equals(item.ExternalId, externalId, StringComparison.Ordinal))) continue;
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
                    ExternalId = externalId
                });
            }

            return Task.CompletedTask;
        }

        if (_overtimeFeedRepository is IOvertimeFeedMutationRepository mutationRepository)
        {
            await mutationRepository.MutateAsync(MutateAsync, cancellationToken);
        }
        else
        {
            var feed = (await _overtimeFeedRepository.ListAsync(cancellationToken)).ToList();
            await MutateAsync(feed);
            await _overtimeFeedRepository.SaveAsync(feed, cancellationToken);
        }
    }

    private async Task<bool> SendResultNotificationAsync(IReadOnlyList<DonateLotteryDrawRecord> wins, CancellationToken cancellationToken)
    {
        if (wins.Count == 0) return true;
        try
        {
            var document = await _settingsStore.ReadAsync(cancellationToken);
            var settings = document?.SystemSettings;
            if (settings is null || string.IsNullOrWhiteSpace(settings.ResultNotificationEmail) || !settings.MailDelivery.HasConfiguration) return true;
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
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to send Donate lottery result notification.");
            return false;
        }
    }

    private async Task<PaymentEventClaimResult> TryClaimPaymentEventAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        if (_paymentEventRepository is IPaymentEventProcessingRepository processingRepository)
        {
            return await processingRepository.TryClaimAsync(paymentEvent, ProcessingLease, cancellationToken);
        }

        // Compatibility fallback for external repositories that implement the
        // original read/append contract. The built-in JSON repository uses the
        // atomic path above.
        var existing = await _paymentEventRepository.ListAsync(cancellationToken);
        if (existing.Any(item => string.Equals(item.ProviderId, paymentEvent.ProviderId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.ExternalId, paymentEvent.ExternalId, StringComparison.Ordinal)))
        {
            return new PaymentEventClaimResult(PaymentEventClaimStatus.AlreadyCompleted, paymentEvent);
        }

        paymentEvent.ProcessingState = "completed";
        await _paymentEventRepository.AppendAsync(paymentEvent, cancellationToken);
        return new PaymentEventClaimResult(PaymentEventClaimStatus.Claimed, paymentEvent);
    }

    private Task PersistPaymentEventAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        return _paymentEventRepository is IPaymentEventProcessingRepository processingRepository
            ? processingRepository.UpdateAsync(paymentEvent, cancellationToken)
            : Task.CompletedTask;
    }

    private async Task MarkPaymentEventFailedAsync(ProcessedPaymentEvent paymentEvent, Exception exception, CancellationToken cancellationToken)
    {
        if (_paymentEventRepository is not IPaymentEventProcessingRepository processingRepository) return;
        try
        {
            paymentEvent.ProcessingState = "failed";
            paymentEvent.ProcessingClaimedAtUtc = null;
            paymentEvent.ProcessingFailureReason = exception.Message;
            await processingRepository.UpdateAsync(paymentEvent, cancellationToken);
        }
        catch (Exception updateException)
        {
            _logger.LogError(updateException, "Unable to persist failed payment event state for {Provider} / {ExternalId}.", paymentEvent.ProviderId, paymentEvent.ExternalId);
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
