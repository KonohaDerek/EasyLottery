using System.Net;
using System.Net.Mail;
using EasyLotteryDomain.Services;

namespace EasyLotteryApi.Endpoints;

internal static class ResultNotificationEndpoints
{
    public static void MapResultNotificationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/result-notification", async (
            ResultNotificationRequest request,
            HttpContext context,
            IEasyLotteryConfigStore configStore,
            ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, request.ResourceKind, request.ResourceId, "control"));
            if (denied is not null) return denied;

            var settings = (await configStore.LoadAsync(context.RequestAborted)).SystemSettings;
            var smtp = settings.MailDelivery;
            var recipient = settings.ResultNotificationEmail.Trim();
            if (string.IsNullOrWhiteSpace(recipient) || !smtp.HasConfiguration) return Results.NoContent();

            using var message = new MailMessage(
                new MailAddress(smtp.FromAddress, smtp.FromName),
                new MailAddress(recipient))
            {
                Subject = request.Subject,
                Body = request.Body
            };
            using var client = new SmtpClient(smtp.SmtpHost, smtp.SmtpPort)
            {
                EnableSsl = smtp.EnableSsl,
                Credentials = new NetworkCredential(smtp.SmtpUsername, smtp.SmtpPassword)
            };
            await client.SendMailAsync(message, context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");
    }
}

public sealed class ResultNotificationRequest
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string ResourceKind { get; set; } = "";
    public string ResourceId { get; set; } = "";
}
