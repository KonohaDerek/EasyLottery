using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace EasyLotteryDomain.Models.Pages
{
    public class ChatRoomCatch
    {
        public string ID { get; set; } = "";

        [Required (ErrorMessage = "請輸入 YT 網址")]
        public string YoutubeUrl { get; set; } = "";

        [Required (ErrorMessage = "通關密語")]
        public string KeyWord { get; set; } = "";

        public DateTimeOffset LastMessageTime { get; set; } = DateTimeOffset.Now;
       
    }
}