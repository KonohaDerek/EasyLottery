using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Youtube.Api.V3;

namespace EasyLotteryDomain.Models.Youtube
{
    public class YoutubeInfo
    {
        public string ChannelId { get; set; } = string.Empty;

        public string ChannelTitle { get; set; } = string.Empty;

        public string ChannelDescription { get; set; } = string.Empty;
    }

    public record LiveChatMessageInfo(
        string UserId,
        string UserName,
        string MessageText,
        LiveChatMessage RawMessage
    );
}
