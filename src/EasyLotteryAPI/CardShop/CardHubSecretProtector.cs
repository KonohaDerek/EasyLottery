using Microsoft.AspNetCore.DataProtection;

namespace EasyLotteryApi.CardShop;

public interface ICardHubSecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}

internal sealed class CardHubSecretProtector(IDataProtectionProvider provider) : ICardHubSecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("EasyLottery.CardHub.SiteSecret.v1");

    public string Protect(string secret) => _protector.Protect(secret);

    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}
