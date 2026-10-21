using System.Globalization;
using EasyLotteryDomain.Services;
using Google.Apis.YouTube.v3.Data;

namespace EasyLotteryApi.Lottery;

public interface ILotteryYouTubeClient
{
    Task<bool> HasConfiguredCredentialsAsync(CancellationToken cancellationToken);
    Task<string?> ResolveLiveChatIdAsync(string liveUrl, CancellationToken cancellationToken);
    Task<IReadOnlyList<LotteryYouTubeMessage>> ListMessagesAsync(string chatId, CancellationToken cancellationToken);
}

public sealed record LotterySuperChatDetails(
    string? AmountMicros,
    string? AmountDisplay,
    string? Currency,
    string? UserComment);

public sealed record LotteryYouTubeMessage(
    string Id,
    string Type,
    string AuthorName,
    string DisplayMessage,
    string PublishedAt,
    string? AuthorImageUrl,
    LotterySuperChatDetails? SuperChat,
    string? AuthorId = null,
    string Platform = "youtube");

public sealed record ResolveYouTubeLiveChatResponse(string ChatId);

public sealed class LotteryYouTubeClient(YouTubeServiceHelper helper) : ILotteryYouTubeClient
{
    public Task<bool> HasConfiguredCredentialsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return helper.HasConfiguredCredentialsAsync();
    }

    public Task<string?> ResolveLiveChatIdAsync(string liveUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return helper.GetLiveChatIDFromLiveUrlAsync(liveUrl);
    }

    public async Task<IReadOnlyList<LotteryYouTubeMessage>> ListMessagesAsync(string chatId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var messages = await helper.ListLiveChatMessageAsync(chatId);
        cancellationToken.ThrowIfCancellationRequested();
        return messages.Select(Project).ToArray();
    }

    internal static LotteryYouTubeMessage Project(LiveChatMessage message)
    {
        var snippet = message.Snippet;
        var superChat = snippet?.SuperChatDetails;
        var authorId = string.IsNullOrWhiteSpace(message.AuthorDetails?.ChannelId)
            ? snippet?.AuthorChannelId
            : message.AuthorDetails.ChannelId;
        return new LotteryYouTubeMessage(
            message.Id ?? "",
            snippet?.Type?.ToString() ?? "",
            message.AuthorDetails?.DisplayName ?? "",
            snippet?.DisplayMessage ?? "",
            snippet?.PublishedAtDateTimeOffset?.ToString("O", CultureInfo.InvariantCulture) ?? "",
            message.AuthorDetails?.ProfileImageUrl,
            superChat is null ? null : new LotterySuperChatDetails(
                superChat.AmountMicros?.ToString(CultureInfo.InvariantCulture),
                superChat.AmountDisplayString,
                superChat.Currency,
                superChat.UserComment),
            authorId,
            "youtube");
    }
}
