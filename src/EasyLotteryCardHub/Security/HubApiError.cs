namespace EasyLotteryCardHub.Security;

public sealed record HubApiError(string Code, string Error);

public sealed record ViewerApiError(string Code, string MessageKey, object? Parameters = null);

public sealed class ViewerServiceException(int statusCode, string code, string messageKey, object? parameters = null)
    : Exception(code)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string MessageKey { get; } = messageKey;
    public object? Parameters { get; } = parameters;
}

public static class HubErrorResults
{
    public static IResult Create(int statusCode, string code, string error) =>
        Results.Json(new HubApiError(code, error), statusCode: statusCode);

    public static IResult Unauthorized(string code = "unauthorized", string error = "Authentication is required.") =>
        Create(StatusCodes.Status401Unauthorized, code, error);

    public static IResult Forbidden(string code, string error) =>
        Create(StatusCodes.Status403Forbidden, code, error);
}
