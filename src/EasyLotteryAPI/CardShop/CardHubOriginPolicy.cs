namespace EasyLotteryApi.CardShop;

internal sealed class CardHubOriginPolicy(IConfiguration configuration, IHostEnvironment environment)
{
    public bool IsAllowed(string value)
    {
        if (!TryGetOrigin(value, out var origin)) return false;

        if (origin.Scheme == Uri.UriSchemeHttp)
            return environment.IsDevelopment() && origin.IsLoopback;

        if (origin.Scheme != Uri.UriSchemeHttps) return false;

        var configured = configuration.GetSection("Shop:TrustedHubOrigins").Get<string[]>() ?? [];
        return configured.Any(candidate => TryGetConfiguredOrigin(candidate, out var trusted)
            && Uri.Compare(origin, trusted, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0);
    }

    public bool TryGetAllowedOrigin(string value, out Uri origin)
    {
        origin = new Uri("about:blank");
        if (!TryGetOrigin(value, out var parsed) || !IsAllowed(value)) return false;
        origin = parsed;
        return true;
    }

    private static bool TryGetOrigin(string value, out Uri origin)
    {
        origin = new Uri("about:blank");
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || uri.UserInfo.Length != 0
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0
            || (uri.AbsolutePath != "/" && uri.AbsolutePath.Length != 0)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return false;
        origin = new Uri(uri.GetLeftPart(UriPartial.Authority));
        return true;
    }

    private static bool TryGetConfiguredOrigin(string value, out Uri origin)
    {
        if (TryGetOrigin(value, out origin) && origin.Scheme == Uri.UriSchemeHttps) return true;
        origin = new Uri("about:blank");
        return false;
    }
}
