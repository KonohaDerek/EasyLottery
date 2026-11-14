namespace EasyLotteryInfrastructure.Shop;

public sealed record HubLinkRecord(
    string HubBaseUrl,
    string SiteClientId,
    string EncryptedSecret,
    string? TenantId,
    string? LastPublishAt);
