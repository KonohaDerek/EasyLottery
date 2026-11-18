using System.Net;
using System.Text;
using EasyLotteryApi.Shop;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class CardHubClientReviewRegressionTests
{
    [TestMethod]
    public async Task TestConnection_ReturnsUnreachableWhenResponseBodyExceedsHttpClientTimeout()
    {
        var completionBound = TimeSpan.FromSeconds(1);
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

        // Four times the 250 ms HttpClient timeout allows 750 ms of CI scheduling overhead
        // while still rejecting a request that only gives up substantially later.
        var completedWithinTimeout = await Task.WhenAny(request, Task.Delay(completionBound)) == request;

        Assert.IsTrue(
            completedWithinTimeout,
            $"The configured HttpClient timeout should include reading the response body and complete within {completionBound}.");
        var result = await request;
        Assert.AreEqual(HubLinkStatuses.HubUnreachable, result);
    }

    [TestMethod]
    public async Task TestConnection_ReturnsUnreachableForNonObjectJsonRoots()
    {
        foreach (var body in new[] { "null", "[]" })
        foreach (var statusCode in new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized })
        {
            using var client = CreateClient(_ => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });

            var result = await client.TestConnectionAsync("https://hub.example.test", "site-id", "secret");

            Assert.AreEqual(
                HubLinkStatuses.HubUnreachable,
                result,
                $"A {statusCode} response with JSON root {body} should be classified as unreachable.");
        }
    }

    private static CardHubClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new HttpClient(new FakeHandler(responseFactory)));

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class StallingReadStream : Stream
    {
        private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => _readStarted.Task;

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
            CancellationToken cancellationToken) => StallReadAsync(cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(StallReadAsync(cancellationToken));

        private async Task<int> StallReadAsync(CancellationToken cancellationToken)
        {
            _readStarted.TrySetResult();
            // Keep the body stalled well beyond the test's acceptance window unless cancelled.
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return 0;
        }
    }
}
