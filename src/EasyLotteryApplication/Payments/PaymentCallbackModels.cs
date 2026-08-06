namespace EasyLotteryApplication.Payments;

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

    /// <summary>
    /// Tracks the payment workflow separately from the provider status. Older event
    /// files do not contain this field and are treated as completed by the default.
    /// </summary>
    public string ProcessingState { get; set; } = "completed";

    public DateTimeOffset? ProcessingClaimedAtUtc { get; set; }

    public DateTimeOffset? ProcessingCompletedAtUtc { get; set; }

    public int ProcessingAttempt { get; set; }

    public string ProcessingFailureReason { get; set; } = "";

    public bool OrderApplied { get; set; }

    public bool DonateApplied { get; set; }

    public bool OvertimeFeedApplied { get; set; }

    public bool ResultNotificationSent { get; set; }
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

public enum PaymentEventClaimStatus
{
    Claimed,
    AlreadyCompleted,
    InProgress
}

public sealed record PaymentEventClaimResult(PaymentEventClaimStatus Status, ProcessedPaymentEvent Event)
{
    public bool IsClaimed => Status == PaymentEventClaimStatus.Claimed;

    public bool IsDuplicate => Status != PaymentEventClaimStatus.Claimed;
}

public interface IPaymentEventRepository
{
    Task<IReadOnlyList<ProcessedPaymentEvent>> ListAsync(CancellationToken cancellationToken = default);
    Task AppendAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional atomic workflow operations implemented by the durable event repository.
/// Keeping this separate preserves compatibility with external provider packages that
/// only implement the original read/append contract.
/// </summary>
public interface IPaymentEventProcessingRepository
{
    Task<PaymentEventClaimResult> TryClaimAsync(
        ProcessedPaymentEvent paymentEvent,
        TimeSpan processingLease,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken = default);
}

public interface IPaymentOrderRepository
{
    Task<IReadOnlyList<RegisteredPaymentOrder>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<RegisteredPaymentOrder> orders, CancellationToken cancellationToken = default);
}

public interface IPaymentOrderMutationRepository
{
    Task MutateAsync(Func<List<RegisteredPaymentOrder>, Task> mutation, CancellationToken cancellationToken = default);
}

public interface IOvertimeFeedRepository
{
    Task<IReadOnlyList<EasyLotteryDomain.Models.Overtime.OvertimeSupportEvent>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<EasyLotteryDomain.Models.Overtime.OvertimeSupportEvent> events, CancellationToken cancellationToken = default);
}

public interface IOvertimeFeedMutationRepository
{
    Task MutateAsync(Func<List<EasyLotteryDomain.Models.Overtime.OvertimeSupportEvent>, Task> mutation, CancellationToken cancellationToken = default);
}
