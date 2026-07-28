namespace EasyLotteryApi;

public sealed class ObsSessionAccess
{
    public const string QueryName = "sessionToken";

    private readonly ObsSessionTokenService _tokens;

    public ObsSessionAccess(ObsSessionTokenService tokens) => _tokens = tokens;

    public bool IsAuthorized(HttpRequest request)
    {
        var token = request.Headers[ObsSessionTokenService.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            token = request.Query[QueryName].ToString();
        }

        return _tokens.IsValid(token);
    }
}
