using System.Net;
using System.Net.Mail;

namespace EasyLotteryApi.Endpoints;

internal static class ResultNotificationEndpoints
{
    public static void MapResultNotificationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/result-notification", async (ResultNotificationRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Recipient) ||
                string.IsNullOrWhiteSpace(request.Smtp.Host) ||
                request.Smtp.Port <= 0 ||
                string.IsNullOrWhiteSpace(request.Smtp.FromAddress))
            {
                return Results.NoContent();
            }

            using var message = new MailMessage(
                new MailAddress(request.Smtp.FromAddress, request.Smtp.FromName),
                new MailAddress(request.Recipient))
            {
                Subject = request.Subject,
                Body = request.Body
            };
            using var client = new SmtpClient(request.Smtp.Host, request.Smtp.Port)
            {
                EnableSsl = request.Smtp.EnableSsl,
                Credentials = new NetworkCredential(request.Smtp.Username, request.Smtp.Password)
            };

            await client.SendMailAsync(message);
            return Results.NoContent();
        });
    }
}

internal sealed class ResultNotificationRequest
{
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public SmtpSettings Smtp { get; set; } = new();
}

internal sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "";
    public bool EnableSsl { get; set; }
}
