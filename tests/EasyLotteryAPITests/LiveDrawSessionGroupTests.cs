using EasyLotteryApi;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class LiveDrawSessionGroupTests
{
    [TestMethod]
    public void GroupName_IsolatedByPublicId()
    {
        var first = LiveDrawSessionGroups.For("pokebox", Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var second = LiveDrawSessionGroups.For("pokebox", Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void GroupName_IsolatedByDrawKind()
    {
        var publicId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Assert.AreNotEqual(
            LiveDrawSessionGroups.For("pokebox", publicId),
            LiveDrawSessionGroups.For("roulette", publicId));
    }
}
