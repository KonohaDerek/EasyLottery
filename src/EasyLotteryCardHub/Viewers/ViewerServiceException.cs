namespace EasyLotteryCardHub.Viewers;

public sealed class ViewerServiceException(
    int statusCode,
    string code,
    string messageKey,
    object? details = null) : Exception(code)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string MessageKey { get; } = messageKey;
    public object? Details { get; } = details;
}

public static class ViewerPolicy
{
    public static bool CanEnterPaidDraw(string ageBracket) => ageBracket == "18_plus";

    public static bool IsReservedHandle(string? handle) => handle is not null &&
        ReservedHandles.Contains(handle.Trim().ToLowerInvariant());

    public static bool IsValidHandle(string? handle) => handle is null ||
        (handle.Length is >= 3 and <= 32 && handle.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '-'));

    private static readonly HashSet<string> ReservedHandles = new(StringComparer.Ordinal)
    {
        "admin", "administrator", "support", "system", "cardhub", "easylottery", "moderator", "official"
    };
}
