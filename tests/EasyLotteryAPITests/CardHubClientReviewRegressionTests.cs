using System.Net;
using EasyLotteryApi.Shop;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class CardHubClientReviewRegressionTests
{
    [TestMethod]
    public async Task TestConnection_ReturnsUnreachableWhenResponseBodyExceedsHttpClientTimeout()
    {
        var bodyStream = new StallingReadStream();
        var httpClient = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(bodyStream)
        }))
        {
            Timeout = TimeSpan.FromMilliseconds(250)
        };
        using var client = new CardHubClient(httpClient);

        var request = client.TestConnectionAsync("https://hub.example.test", "site-id", "secret");
        var readStarted = await Task.WhenAny(bodyStream.ReadStarted, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(bodyStream.ReadStarted, readStarted, "The Hub response body should begin streaming.");

        var completedWithinTimeout = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(2))) == request;
        bodyStream.ReleaseRead();
        var result = await request;

        Assert.IsTrue(completedWithinTimeout, "The configured HttpClient timeout should include reading the response body.");
        Assert.AreEqual(HubLinkStatuses.HubUnreachable, result);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class StallingReadStream : Stream
    {
        private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseRead = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => _readStarted.Task;

        public void ReleaseRead() => _releaseRead.TrySetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) => WaitForReleaseAsync(cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(WaitForReleaseAsync(cancellationToken));

        private async Task<int> WaitForReleaseAsync(CancellationToken cancellationToken)
        {
            _readStarted.TrySetResult();
            await _releaseRead.Task.WaitAsync(cancellationToken);
            return 0;
        }
    }
}
