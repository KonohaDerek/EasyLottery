using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services
{
    public sealed class ResultNotificationService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<ResultNotificationService> _logger;
        private readonly IEasyLotteryConfigStore _configStore;

        public ResultNotificationService(
            IEasyLotteryConfigStore configStore,
            IJSRuntime jsRuntime,
            ILogger<ResultNotificationService> logger)
        {
            _configStore = configStore;
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
                var document = await _configStore.LoadAsync(cancellationToken);
                var resourceKind = record.ActivityType == ActivityResultType.PokeBox ? "pokebox" : "roulette";
                var resourceId = record.ActivityType == ActivityResultType.PokeBox
                    ? document.PokeTemplates.FirstOrDefault(item => item.Id == record.TemplateId)?.PublicId.ToString()
                    : document.RouletteTemplates.FirstOrDefault(item => item.Id == record.TemplateId)?.PublicId.ToString();
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
