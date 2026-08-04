using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services
{
    public sealed class ResultNotificationService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<ResultNotificationService> _logger;
        public ResultNotificationService(
            IJSRuntime jsRuntime,
            ILogger<ResultNotificationService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async Task NotifyActivityResultAsync(ActivityResultRecord record, CancellationToken cancellationToken = default)
        {
            if (record == null)
            {
                return;
            }

            try
            {
                var resourceKind = record.ActivityType == ActivityResultType.PokeBox ? "pokebox" : "roulette";
                var resourceId = record.TemplatePublicId == Guid.Empty ? "" : record.TemplatePublicId.ToString();
                if (string.IsNullOrWhiteSpace(resourceId)) return;

                var request = new EmailRequest
                {
                    Subject = ActivityResultNotificationFormatter.BuildSubject(record),
                    Body = ActivityResultNotificationFormatter.BuildBody(record),
                    ResourceKind = resourceKind,
                    ResourceId = resourceId
                };

                await _jsRuntime.InvokeVoidAsync("easyLotteryMail.send", cancellationToken, request);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Failed to send result notification mail through the browser bridge.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send result notification mail.");
            }
        }

        public sealed class EmailRequest
        {
            public string Subject { get; set; } = "";

            public string Body { get; set; } = "";
            public string ResourceKind { get; set; } = "";
            public string ResourceId { get; set; } = "";

        }
    }
}
