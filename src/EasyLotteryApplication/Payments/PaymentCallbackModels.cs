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

public interface IPaymentEventRepository
{
    Task<IReadOnlyList<ProcessedPaymentEvent>> ListAsync(CancellationToken cancellationToken = default);
    Task AppendAsync(ProcessedPaymentEvent paymentEvent, CancellationToken cancellationToken = default);
}

public interface IPaymentOrderRepository
{
    Task<IReadOnlyList<RegisteredPaymentOrder>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<RegisteredPaymentOrder> orders, CancellationToken cancellationToken = default);
}

public interface IOvertimeFeedRepository
{
    Task<IReadOnlyList<EasyLotteryDomain.Models.Overtime.OvertimeSupportEvent>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<EasyLotteryDomain.Models.Overtime.OvertimeSupportEvent> events, CancellationToken cancellationToken = default);
}
