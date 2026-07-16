using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services
{
    public sealed class ResultNotificationService
    {
        private readonly SystemSettingsService _systemSettingsService;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<ResultNotificationService> _logger;

        public ResultNotificationService(
            SystemSettingsService systemSettingsService,
            IJSRuntime jsRuntime,
            ILogger<ResultNotificationService> logger)
        {
            _systemSettingsService = systemSettingsService;
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
                var settings = await _systemSettingsService.GetSettingsAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(settings.ResultNotificationEmail) || !settings.MailDelivery.HasConfiguration)
                {
                    return;
                }

                var request = new EmailRequest
                {
                    Recipient = settings.ResultNotificationEmail.Trim(),
                    Subject = ActivityResultNotificationFormatter.BuildSubject(record),
                    Body = ActivityResultNotificationFormatter.BuildBody(record),
                    Smtp = new SmtpRequest
                    {
                        Host = settings.MailDelivery.SmtpHost,
                        Port = settings.MailDelivery.SmtpPort,
                        Username = settings.MailDelivery.SmtpUsername,
                        Password = settings.MailDelivery.SmtpPassword,
                        FromAddress = settings.MailDelivery.FromAddress,
                        FromName = settings.MailDelivery.FromName,
                        EnableSsl = settings.MailDelivery.EnableSsl
                    }
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
            public string Recipient { get; set; } = "";

            public string Subject { get; set; } = "";

            public string Body { get; set; } = "";

            public SmtpRequest Smtp { get; set; } = new();
        }

        public sealed class SmtpRequest
        {
            public string Host { get; set; } = "";

            public int Port { get; set; }

            public string Username { get; set; } = "";

            public string Password { get; set; } = "";

            public string FromAddress { get; set; } = "";

            public string FromName { get; set; } = "";

            public bool EnableSsl { get; set; }
        }
    }
}