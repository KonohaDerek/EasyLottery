using EasyLotteryDomain.Models.Obs;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class ObsAssetLimitsTests
{
    [TestMethod]
    public void ReadMaxAssetBytes_UsesDefaultAndClampsConfiguredValues()
    {
        Assert.AreEqual(ObsAssetLimits.DefaultMaxAssetBytes, ObsAssetLimits.ReadMaxAssetBytes(null));
        Assert.AreEqual(ObsAssetLimits.MinimumAssetBytes, ObsAssetLimits.ReadMaxAssetBytes("1"));
        Assert.AreEqual(ObsAssetLimits.MaximumConfiguredAssetBytes, ObsAssetLimits.ReadMaxAssetBytes("999999999"));
        Assert.AreEqual(2 * 1024 * 1024, ObsAssetLimits.ReadMaxAssetBytes("2097152"));
    }
}
