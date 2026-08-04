using EasyLotteryWasm.Layout;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class NavigationQueryTests
{
    [TestMethod]
    public void TryGetInt_ReadsLegacyEditorTarget()
    {
        Assert.IsTrue(NavigationQuery.TryGetInt("?edit=42", "edit", out var id));
        Assert.AreEqual(42, id);
    }

    [TestMethod]
    public void TryGetInt_HandlesEncodedValuesAndRejectsInvalidInput()
    {
        Assert.IsTrue(NavigationQuery.TryGetInt("?from=market&edit=0", "edit", out var newId));
        Assert.AreEqual(0, newId);
        Assert.IsFalse(NavigationQuery.TryGetInt("?edit=not-an-id", "edit", out _));
        Assert.IsFalse(NavigationQuery.TryGetInt("?preview=1", "edit", out _));
    }
}
