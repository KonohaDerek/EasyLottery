using EasyLotteryCardHub.Services;

namespace EasyLotteryCardHub.Tests;

[TestClass]
public sealed class ViewerCodeRequestRateLimiterTests
{
    [TestMethod]
    public void PruningExpiredEmailRequestKeepsNewerRequestsInTheBucket()
    {
        var limiter = new ViewerCodeRequestRateLimiter();
        var start = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

        Assert.IsTrue(limiter.TryAcquire("email", "ip", start, out _));
        for (var minute = 50; minute <= 53; minute++)
            Assert.IsTrue(limiter.TryAcquire("email", "ip", start.AddMinutes(minute), out _));

        Assert.IsTrue(limiter.TryAcquire("email", "ip", start.AddMinutes(60), out _));
        Assert.IsFalse(limiter.TryAcquire("email", "ip", start.AddMinutes(61), out _));
    }

    [TestMethod]
    public void PruningExpiredIpRequestKeepsNewerRequestsInTheBucket()
    {
        var limiter = new ViewerCodeRequestRateLimiter();
        var start = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

        Assert.IsTrue(limiter.TryAcquire("email-0", "ip", start, out _));
        for (var request = 1; request <= 19; request++)
            Assert.IsTrue(limiter.TryAcquire($"email-{request}", "ip", start.AddMinutes(50), out _));

        Assert.IsTrue(limiter.TryAcquire("email-20", "ip", start.AddMinutes(60), out _));
        Assert.IsFalse(limiter.TryAcquire("email-21", "ip", start.AddMinutes(61), out _));
    }
}
