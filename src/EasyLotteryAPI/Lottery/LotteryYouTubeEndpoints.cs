using System.Text.RegularExpressions;
using EasyLotteryApi.Security;

namespace EasyLotteryApi.Lottery;

internal static partial class LotteryYouTubeEndpoints
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,256}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChatIdPattern();

    public static void MapLotteryYouTubeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/lottery/youtube/status", async (HttpContext context, ObsSessionAccess access, ILotteryYouTubeClient youtube) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try
            {
                return Results.Ok(new YouTubeCredentialStatus(await youtube.HasConfiguredCredentialsAsync(context.RequestAborted)));
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "YouTube 設定目前無法讀取。", detail: "請稍後重試或檢查伺服器設定。");
            }
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/lottery/youtube/live-chat/resolve", async (ResolveYouTubeLiveChatRequest request, HttpContext context, ObsSessionAccess access, ILotteryYouTubeClient youtube) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            if (!IsAllowedYouTubeUrl(request.Url)) return Results.BadRequest(new { error = "請輸入有效的 YouTube 直播網址。" });

            try
            {
                var chatId = await youtube.ResolveLiveChatIdAsync(request.Url, context.RequestAborted);
                return string.IsNullOrWhiteSpace(chatId)
                    ? Results.NotFound(new { error = "這個網址目前沒有進行中的直播聊天室。" })
                    : Results.Ok(new ResolveYouTubeLiveChatResponse(chatId));
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "YouTube API 尚未設定。", detail: "請由管理設定完成 YouTube API Key 設定。");
            }
            catch
            {
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "無法取得 YouTube 直播聊天室。", detail: "請確認直播網址及 YouTube API 狀態後重試。");
            }
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/lottery/youtube/live-chat/{chatId}/messages", async (string chatId, HttpContext context, ObsSessionAccess access, ILotteryYouTubeClient youtube) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            if (!ChatIdPattern().IsMatch(chatId)) return Results.BadRequest(new { error = "YouTube 聊天室 ID 格式無效。" });

            try
            {
                return Results.Ok(await youtube.ListMessagesAsync(chatId, context.RequestAborted));
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "YouTube API 尚未設定。", detail: "請由管理設定完成 YouTube API Key 設定。");
            }
            catch
            {
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "YouTube 聊天室讀取失敗。", detail: "請稍後重試。");
            }
        }).RequireRateLimiting("sensitive");
    }

    private static bool IsAllowedYouTubeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            return false;
        return uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("m.youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record YouTubeCredentialStatus(bool Configured);
}

public sealed record ResolveYouTubeLiveChatRequest(string Url);
