using System.Security.Cryptography;
using EasyLotteryInfrastructure.Shop;
using Microsoft.AspNetCore.DataProtection;

namespace EasyLotteryApi.Shop;

public sealed class HubLinkSettingsService(
    SqliteHubLinkRepository repository,
    IDataProtectionProvider dataProtectionProvider,
    CardHubClient hubClient)
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("EasyLottery.CardHub.SiteClientSecret.v1");

    public async Task<HubLinkResponse> ReadAsync(CancellationToken cancellationToken)
    {
        var saved = await repository.ReadAsync(cancellationToken);
        if (saved is null) return new HubLinkResponse();

        string secret;
        try
        {
            secret = _protector.Unprotect(saved.EncryptedSecret);
        }
        catch (CryptographicException)
        {
            return ToResponse(saved, HubLinkStatuses.CredentialsInvalid);
        }

        var status = await hubClient.TestConnectionAsync(saved.HubBaseUrl, saved.SiteClientId, secret, cancellationToken);
        return ToResponse(saved, status);
    }

    public async Task<HubLinkResponse> TestAsync(HubLinkWriteRequest request, CancellationToken cancellationToken)
    {
        var hubBaseUrl = NormalizeHubBaseUrl(request.HubBaseUrl);
        var siteClientId = NormalizeClientId(request.SiteClientId);
        var secret = request.ClientSecret;
        if (secret?.Length > 4096)
            throw new ArgumentException("Client secret 長度超過限制。");
        var saved = await repository.ReadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(secret))
        {
            if (saved is null
                || !string.Equals(saved.SiteClientId, siteClientId, StringComparison.Ordinal)
                || !string.Equals(saved.HubBaseUrl, hubBaseUrl, StringComparison.Ordinal))
                return new HubLinkResponse { HubBaseUrl = hubBaseUrl, SiteClientId = siteClientId };
            try
            {
                secret = _protector.Unprotect(saved.EncryptedSecret);
            }
            catch (CryptographicException)
            {
                return ToResponse(saved, HubLinkStatuses.CredentialsInvalid);
            }
        }

        if (string.IsNullOrWhiteSpace(secret))
            return new HubLinkResponse { HubBaseUrl = hubBaseUrl, SiteClientId = siteClientId };

        var status = await hubClient.TestConnectionAsync(hubBaseUrl, siteClientId, secret, cancellationToken);
        return new HubLinkResponse
        {
            HubBaseUrl = hubBaseUrl,
            SiteClientId = siteClientId,
            HasClientSecret = saved is not null
                && string.Equals(saved.SiteClientId, siteClientId, StringComparison.Ordinal)
                && string.Equals(saved.HubBaseUrl, hubBaseUrl, StringComparison.Ordinal),
            Status = status,
            TenantId = saved?.TenantId,
            LastPublishAt = saved?.LastPublishAt
        };
    }

    public async Task<HubLinkResponse> SaveAsync(HubLinkWriteRequest request, CancellationToken cancellationToken)
    {
        var hubBaseUrl = NormalizeHubBaseUrl(request.HubBaseUrl);
        var siteClientId = NormalizeClientId(request.SiteClientId);
        var saved = await repository.ReadAsync(cancellationToken);
        string encryptedSecret;

        if (string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            if (saved is null
                || !string.Equals(saved.SiteClientId, siteClientId, StringComparison.Ordinal)
                || !string.Equals(saved.HubBaseUrl, hubBaseUrl, StringComparison.Ordinal))
                throw new ArgumentException("更換 Hub 網址或 site client id 時，請重新輸入 Card Hub 提供的 client secret。");
            encryptedSecret = saved.EncryptedSecret;
        }
        else
        {
            if (request.ClientSecret.Length > 4096)
                throw new ArgumentException("Client secret 長度超過限制。");
            encryptedSecret = _protector.Protect(request.ClientSecret);
        }

        var record = new HubLinkRecord(
            hubBaseUrl,
            siteClientId,
            encryptedSecret,
            saved?.TenantId,
            saved?.LastPublishAt);
        await repository.SaveAsync(record, cancellationToken);
        var secret = _protector.Unprotect(record.EncryptedSecret);
        var status = await hubClient.TestConnectionAsync(hubBaseUrl, siteClientId, secret, cancellationToken);
        return ToResponse(record, status);
    }

    public static string NormalizeHubBaseUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 2048
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Hub Base URL 必須是有效的 HTTPS 網址（本機測試可使用 localhost HTTP）。");
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string NormalizeClientId(string value)
    {
        var clientId = value?.Trim() ?? "";
        if (clientId.Length is 0 or > 200)
            throw new ArgumentException("請輸入有效的 site client id。");
        return clientId;
    }

    private static HubLinkResponse ToResponse(HubLinkRecord record, string status) => new()
    {
        HubBaseUrl = record.HubBaseUrl,
        SiteClientId = record.SiteClientId,
        HasClientSecret = !string.IsNullOrWhiteSpace(record.EncryptedSecret),
        Status = status,
        TenantId = record.TenantId,
        LastPublishAt = record.LastPublishAt
    };
}
