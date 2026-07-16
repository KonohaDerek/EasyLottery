using System.Text;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services
{
    public static class ActivityResultNotificationFormatter
    {
        public static string BuildSubject(ActivityResultRecord record)
        {
            return $"EasyLottery 結果通知 - {record.ActivityName}";
        }

        public static string BuildBody(ActivityResultRecord record)
        {
            var builder = new StringBuilder();
            builder.AppendLine("EasyLottery 結果通知");
            builder.AppendLine($"活動名稱：{record.ActivityName}");
            builder.AppendLine($"活動時間：{record.ActivityDateUtc.ToLocalTime():yyyy/MM/dd HH:mm:ss}");
            builder.AppendLine($"摘要：{record.Summary}");

            if (record.Items.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("結果明細：");

                foreach (var item in record.Items.OrderBy(item => item.Order))
                {
                    builder.AppendLine($"{item.Order}. {item.Name} - {item.Description}");
                }
            }

            return builder.ToString();
        }
    }
}