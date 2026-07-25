using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

namespace EasyLotteryDomainTests.Services;

[TestClass]
public sealed class YamlRepositoryMapperTests
{
    [TestMethod]
    public void SplitAndMerge_PreservesActivityDatesAndResults()
    {
        var start = new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);
        var source = new EasyLotteryConfigDocument
        {
            DonateLotteryActivities = [new DonateLotteryActivity { Id = 4, StartsAtUtc = start, EndsAtUtc = end, Name = "一番賞" }],
            ActivityResults = [new ActivityResultRecord { Id = 9, ActivityName = "一番賞", ActivityDateUtc = start.UtcDateTime }]
        };

        var parts = YamlRepositoryMapper.Split(source);
        var merged = YamlRepositoryMapper.Merge(parts.Settings, parts.Activities, parts.Results);

        Assert.AreEqual(start, merged.DonateLotteryActivities[0].StartsAtUtc);
        Assert.AreEqual(end, merged.DonateLotteryActivities[0].EndsAtUtc);
        Assert.AreEqual(9, merged.ActivityResults[0].Id);
    }
}
