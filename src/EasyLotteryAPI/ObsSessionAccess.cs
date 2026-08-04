using System.Security.Claims;

namespace EasyLotteryApi;

public enum ApiAccessDecision { Allowed, Unauthorized, Forbidden }

public sealed class ObsSessionAccess
{
    public const string QueryName = "sessionToken";
    private readonly ObsSessionTokenService _tokens;

    public ObsSessionAccess(ObsSessionTokenService tokens) => _tokens = tokens;

    public ApiAccessDecision RequireAdmin(HttpRequest request)
    {
        var token = ReadHeaderToken(request);
        var principal = _tokens.Validate(token);
        if (principal is null) return ApiAccessDecision.Unauthorized;
        return principal.FindFirst("token_use")?.Value == ObsSessionTokenService.AdminUse
            ? ApiAccessDecision.Allowed
            : ApiAccessDecision.Forbidden;
    }

    public ApiAccessDecision RequireObs(HttpRequest request, string kind, string resourceId, string scope)
    {
        var headerToken = ReadHeaderToken(request);
        var token = string.IsNullOrWhiteSpace(headerToken) ? ReadQueryToken(request) : headerToken;
        var principal = _tokens.Validate(token);
        if (principal is null) return ApiAccessDecision.Unauthorized;
        if (string.IsNullOrWhiteSpace(headerToken) && principal.FindFirst("token_use")?.Value != ObsSessionTokenService.ObsUse)
            return ApiAccessDecision.Forbidden;
        return _tokens.CanAccessObs(token, kind, resourceId, scope)
            ? ApiAccessDecision.Allowed
            : ApiAccessDecision.Forbidden;
    }

    public ClaimsPrincipal? ReadPrincipal(HttpRequest request) => _tokens.Validate(ReadAnyToken(request));

    public static IResult? DeniedResult(ApiAccessDecision decision) => decision switch
    {
        ApiAccessDecision.Unauthorized => Results.Unauthorized(),
        ApiAccessDecision.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        _ => null
    };

    private static string ReadHeaderToken(HttpRequest request) => request.Headers[ObsSessionTokenService.HeaderName].ToString();
    private static string ReadQueryToken(HttpRequest request) => request.Query[QueryName].ToString();
    private static string ReadAnyToken(HttpRequest request) =>
        string.IsNullOrWhiteSpace(ReadHeaderToken(request)) ? ReadQueryToken(request) : ReadHeaderToken(request);
}
