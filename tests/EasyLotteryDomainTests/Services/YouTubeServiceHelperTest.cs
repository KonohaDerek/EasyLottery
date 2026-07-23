using EasyLotteryDomain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EasyLotteryDomainTests.Services;

[TestClass]
public class YouTubeServiceHelperTest
{
    [TestMethod]
    public void GetYouTubeLiveId_ParsesWatchUrl()
    {
        var actual = YouTubeServiceHelper.GetYouTubeLiveID("https://www.youtube.com/watch?v=-L9cBf3oAO0");

        Assert.AreEqual("-L9cBf3oAO0", actual);
    }

    [TestMethod]
    public void GetYouTubeLiveId_ParsesLiveUrl()
    {
        var actual = YouTubeServiceHelper.GetYouTubeLiveID("https://www.youtube.com/live/M4_eD9CLTaI");

        Assert.AreEqual("M4_eD9CLTaI", actual);
    }

    [TestMethod]
    public async Task GetYoutubeLiveInfoAsync_WithoutApiKey_ThrowsInvalidOperationException()
    {
        var helper = CreateHelperWithoutApiKey();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => helper.GetYoutubeLiveInfoAsync("zrSJ7p5m0Bk"));

        Assert.AreEqual("YouTube API key is required. Please configure the API key first.", exception.Message);
    }

    [TestMethod]
    public async Task ListLiveChatMessageAsync_WithoutApiKey_ThrowsInvalidOperationException()
    {
        var helper = CreateHelperWithoutApiKey();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => helper.ListLiveChatMessageAsync("chat-id"));

        Assert.AreEqual("YouTube API key is required. Please configure the API key first.", exception.Message);
    }

    private static YouTubeServiceHelper CreateHelperWithoutApiKey()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new YouTubeServiceHelper(configuration, new Logger<YouTubeServiceHelper>(NullLoggerFactory.Instance));
    }
}
