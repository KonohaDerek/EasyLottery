using EasyLotteryDomain.Models.Config;
using EasyLotteryWasm.Models;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class DonateObsAnimationCatalogTests
{
    [TestMethod]
    [DataRow(DonateLotteryAnimation.IchibanKuji, "animation-ichiban", "ichiban")]
    [DataRow(DonateLotteryAnimation.Gacha, "animation-gacha", "gacha")]
    [DataRow(DonateLotteryAnimation.Garagara, "animation-garagara", "garagara")]
    [DataRow(DonateLotteryAnimation.MoneyBox, "animation-money-box", "moneybox")]
    [DataRow(DonateLotteryAnimation.ScratchCard, "animation-scratch-card", "scratch-card")]
    public void Resolve_MapsEverySupportedAnimationToUniqueObsScene(DonateLotteryAnimation animation, string cssClass, string sceneKey)
    {
        var presentation = DonateObsAnimationCatalog.Resolve(animation);

        Assert.AreEqual(cssClass, presentation.CssClass);
        Assert.AreEqual(sceneKey, presentation.SceneKey);
    }
}
