using System.Security.Cryptography;
using System.Text;
using EasyLotteryCardHub.Security;

namespace EasyLotteryCardHub.Services;

public sealed record ViewerLoginCodeEmail(
    string SubjectKey,
    string Subject,
    IReadOnlyList<string> BodyKeys,
    string Body);

public interface IViewerEmailSender
{
    Task SendLoginCodeAsync(string recipient, ViewerLoginCodeEmail email, CancellationToken cancellationToken);
}

public sealed class DevelopmentViewerEmailSender(IHostEnvironment environment, ILogger<DevelopmentViewerEmailSender> logger) : IViewerEmailSender
{
    public Task SendLoginCodeAsync(string recipient, ViewerLoginCodeEmail email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            throw new ViewerServiceException(StatusCodes.Status503ServiceUnavailable, "viewer_email_unavailable", "viewer.code.error.network");

        logger.LogInformation("Development viewer email to {Recipient}: {Subject}\n{Body}", recipient, email.Subject, email.Body);
        return Task.CompletedTask;
    }
}

public static class ViewerEmailCopyDeck
{
    private const string SubjectKey = "viewer.loginCode.email.subject";
    private const string StreamerSubjectKey = "viewer.loginCode.email.subject.streamer";
    private const string IntroKey = "viewer.loginCode.email.body.intro";
    private const string StreamerIntroKey = "viewer.loginCode.email.body.intro.streamer";
    private const string CodeKey = "viewer.loginCode.email.body.code";
    private const string ExpiryKey = "viewer.loginCode.email.body.expiry";
    private const string SafetyKey = "viewer.loginCode.email.body.safety";
    private const string SignatureKey = "viewer.loginCode.email.signature";
    private const string FooterKey = "viewer.loginCode.email.footer";

    public static ViewerLoginCodeEmail Create(string code, string? streamerName)
    {
        var hasStreamer = !string.IsNullOrWhiteSpace(streamerName);
        var intro = hasStreamer
            ? $"你正在從 {streamerName!.Trim()} 的卡牌活動登入 Card Hub。"
            : "你正在登入 Card Hub。";
        var subject = hasStreamer
            ? $"{streamerName!.Trim()} 卡牌收藏：你的登入驗證碼 {code}"
            : $"你的 Card Hub 登入驗證碼：{code}";
        var body = string.Join('\n',
            intro,
            $"你的驗證碼是：{code}",
            "驗證碼 10 分鐘內有效，只能使用一次。",
            "如果不是你本人操作請忽略這封信，你的帳號不會有任何變更。請勿將驗證碼提供給任何人，包括實況主或客服人員。",
            "EasyLottery Card Hub",
            "這是系統自動寄出的信件，請勿直接回覆。");
        var bodyKeys = hasStreamer
            ? new[] { StreamerIntroKey, CodeKey, ExpiryKey, SafetyKey, SignatureKey, FooterKey }
            : new[] { IntroKey, CodeKey, ExpiryKey, SafetyKey, SignatureKey, FooterKey };
        return new ViewerLoginCodeEmail(hasStreamer ? StreamerSubjectKey : SubjectKey, subject, bodyKeys, body);
    }
}

public static class ViewerSensitiveHasher
{
    public static string HashEmail(string normalizedEmail, byte[] key) => Hash("email:", normalizedEmail, key);
    public static string HashCode(string code, byte[] key) => Hash("code:", code, key);
    public static string HashIp(string ipAddress, byte[] key) => Hash("ip:", ipAddress, key);

    private static string Hash(string purpose, string value, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(purpose + value)));
    }
}

public sealed class ViewerCodeRequestRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private const int EmailLimit = 5;
    private const int IpLimit = 20;
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _requests = new(StringComparer.Ordinal);
    private DateTimeOffset _nextPruneAt = DateTimeOffset.MinValue;

    public bool TryAcquire(string emailHash, string ipHash, DateTimeOffset now, out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            if (now >= _nextPruneAt)
            {
                var cutoff = now - Window;
                foreach (var key in _requests.Where(pair => !pair.Value.TryPeek(out var createdAt) || createdAt <= cutoff)
                    .Select(pair => pair.Key).ToArray())
                    _requests.Remove(key);
                _nextPruneAt = now.AddMinutes(5);
            }

            var emailQueue = GetActiveQueue("email:" + emailHash, now);
            var ipQueue = GetActiveQueue("ip:" + ipHash, now);
            if (emailQueue.Count >= EmailLimit)
            {
                retryAfter = emailQueue.Peek().Add(Window) - now;
                return false;
            }
            if (ipQueue.Count >= IpLimit)
            {
                retryAfter = ipQueue.Peek().Add(Window) - now;
                return false;
            }

            emailQueue.Enqueue(now);
            ipQueue.Enqueue(now);
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    private Queue<DateTimeOffset> GetActiveQueue(string key, DateTimeOffset now)
    {
        if (!_requests.TryGetValue(key, out var queue))
            _requests[key] = queue = new Queue<DateTimeOffset>();
        while (queue.TryPeek(out var createdAt) && createdAt <= now - Window) queue.Dequeue();
        if (queue.Count == 0) _requests.Remove(key);
        if (queue.Count == 0) _requests[key] = queue = new Queue<DateTimeOffset>();
        return queue;
    }
}
