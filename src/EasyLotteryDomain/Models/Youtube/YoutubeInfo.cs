using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Youtube.Api.V3;

namespace EasyLotteryDomain.Models.Youtube
{
    public class YoutubeInfo
    {
        public string ChannelId { get; set; }

        public string ChannelTitle { get; set; }

        public string ChannelDescription { get; set; }
    }

    public record LiveChatMessageInfo(
        string UserId,
        string UserName,
        string MessageText,
        LiveChatMessage RawMessage
    );
}