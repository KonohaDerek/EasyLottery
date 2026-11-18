namespace EasyLotteryApi.Shop;

public sealed class HubLinkWriteRequest
{
    public string HubBaseUrl { get; init; } = "";
    public string SiteClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
}

public sealed class HubLinkResponse
{
    public string HubBaseUrl { get; init; } = "";
    public string SiteClientId { get; init; } = "";
    public bool HasClientSecret { get; init; }
    public string Status { get; init; } = HubLinkStatuses.NotLinked;
    public string? TenantId { get; init; }
    public string? LastPublishAt { get; init; }
}
