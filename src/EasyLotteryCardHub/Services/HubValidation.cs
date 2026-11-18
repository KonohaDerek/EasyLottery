using System.Net.Mail;
using System.Text.RegularExpressions;

namespace EasyLotteryCardHub.Services;

public static partial class HubValidation
{
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex TenantSlugPattern();

    public static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 254
            || !MailAddress.TryCreate(normalized, out var address)
            || !string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
            throw new HubServiceException(StatusCodes.Status400BadRequest, "invalid_email", "A valid email address is required.");
        return normalized;
    }

    public static string NormalizeSlug(string? slug)
    {
        var normalized = slug?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length > 63 || !TenantSlugPattern().IsMatch(normalized))
            throw new HubServiceException(StatusCodes.Status400BadRequest, "invalid_tenant_slug", "The tenant slug must use lowercase letters, digits, and internal hyphens.");
        return normalized;
    }

    public static string NormalizeDisplayName(string? name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 100)
            throw new HubServiceException(StatusCodes.Status400BadRequest, "invalid_display_name", "The display name must be between 1 and 100 characters.");
        return normalized;
    }

    public static string NormalizeSiteName(string? name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 100)
            throw new HubServiceException(StatusCodes.Status400BadRequest, "invalid_site_name", "The site name must be between 1 and 100 characters.");
        return normalized;
    }

    public static string NormalizeSiteUrl(string? value, bool allowLoopbackHttp)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !(allowLoopbackHttp && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback))
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new HubServiceException(StatusCodes.Status400BadRequest, "invalid_site_url", "The site URL must be an HTTPS base URL.");

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}

public sealed class HubServiceException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
