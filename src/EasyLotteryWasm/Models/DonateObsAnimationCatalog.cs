using EasyLotteryDomain.Models.Config;

namespace EasyLotteryWasm.Models;

public sealed record DonateObsAnimationPresentation(string CssClass, string SceneKey);

public static class DonateObsAnimationCatalog
{
    public static DonateObsAnimationPresentation Resolve(DonateLotteryAnimation animation) => animation switch
    {
        DonateLotteryAnimation.Gacha => new("animation-gacha", "gacha"),
        DonateLotteryAnimation.Garagara => new("animation-garagara", "garagara"),
        DonateLotteryAnimation.MoneyBox => new("animation-money-box", "moneybox"),
        DonateLotteryAnimation.ScratchCard => new("animation-scratch-card", "scratch-card"),
        _ => new("animation-ichiban", "ichiban")
    };
}
