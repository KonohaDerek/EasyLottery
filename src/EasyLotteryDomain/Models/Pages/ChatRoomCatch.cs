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

        public string WhiteList { get; set; } = "";

        public string BlackList { get; set; } = "";

        [Range(0, 86400, ErrorMessage = "同帳號冷卻需為 0 或以上的秒數")]
        public int CooldownSeconds { get; set; } = 0;

        public DateTimeOffset LastMessageTime { get; set; } = DateTimeOffset.Now;

        public IReadOnlyList<string> GetKeywordTokens() => SplitRuleList(KeyWord);

        public IReadOnlyList<string> GetWhiteListTokens() => SplitRuleList(WhiteList);

        public IReadOnlyList<string> GetBlackListTokens() => SplitRuleList(BlackList);

        public bool MatchesKeyword(string? messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return false;
            }

            var tokens = GetKeywordTokens();
            if (tokens.Count == 0)
            {
                return false;
            }

            return tokens.Any(token =>
                messageText.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsMessageAllowed(
            string? authorName,
            string? messageText,
            DateTimeOffset publishedAt,
            IReadOnlyDictionary<string, DateTimeOffset> lastAcceptedMessageTimes,
            out string reason)
        {
            reason = "";

            var normalizedAuthor = Normalize(authorName);
            if (string.IsNullOrWhiteSpace(normalizedAuthor))
            {
                reason = "找不到作者名稱";
                return false;
            }

            if (!MatchesKeyword(messageText))
            {
                reason = "未符合關鍵字";
                return false;
            }

            var blackList = new HashSet<string>(GetBlackListTokens(), StringComparer.OrdinalIgnoreCase);
            if (blackList.Contains(normalizedAuthor))
            {
                reason = "命中黑名單";
                return false;
            }

            var whiteList = GetWhiteListTokens();
            if (whiteList.Count > 0 &&
                !whiteList.Contains(normalizedAuthor, StringComparer.OrdinalIgnoreCase))
            {
                reason = "不在白名單";
                return false;
            }

            if (CooldownSeconds > 0 &&
                lastAcceptedMessageTimes.TryGetValue(normalizedAuthor, out var lastAcceptedAt))
            {
                var elapsed = publishedAt - lastAcceptedAt;
                if (elapsed < TimeSpan.FromSeconds(CooldownSeconds))
                {
                    reason = "同帳號冷卻中";
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<string> SplitRuleList(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(new[] { '\r', '\n', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string Normalize(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
