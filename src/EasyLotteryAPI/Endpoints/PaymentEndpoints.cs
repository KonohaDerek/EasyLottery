using System.Net;
using System.Text;
using EasyLotteryApplication.Payments;
using EasyLotteryApi;
using EasyLotteryApi.Payments;

namespace EasyLotteryApi.Endpoints;

internal static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/payments/{providerId}/notify", async (string providerId, HttpContext context, PaymentCallbackProcessor callbacks) =>
        {
            var body = await HttpRequestBodyReader.ReadTextAsync(context.Request, context.RequestAborted);
            var headers = context.Request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            var result = await callbacks.ProcessAsync(providerId, new PaymentNotificationRequest
            {
                ContentType = context.Request.ContentType ?? "",
                Body = body,
                Headers = headers
            }, context.RequestAborted);

            if (!result.Accepted)
            {
                return Results.BadRequest();
            }

            var acknowledgement = providerId.Trim().Equals("ecpay", StringComparison.OrdinalIgnoreCase)
                ? "1|OK"
                : "OK";
            return Results.Text(acknowledgement, "text/plain", Encoding.UTF8);
        });

        app.MapPost("/api/payments/orders", async (PaymentOrderRegistrationRequest request, HttpContext context, PaymentCallbackProcessor callbacks, AdminAccess adminAccess) =>
        {
            if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            try { return Results.Created($"/api/payments/orders/{request.MerchantOrderNo}", await callbacks.RegisterOrderAsync(request, context.RequestAborted)); }
            catch (ArgumentException exception)
            {
                app.Logger.LogWarning(exception, "Invalid payment order registration request for merchant order {MerchantOrderNo}.", request.MerchantOrderNo);
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                app.Logger.LogWarning(exception, "Payment order registration conflict for merchant order {MerchantOrderNo}.", request.MerchantOrderNo);
                return Results.Conflict(new { error = exception.Message });
            }
        });

        app.MapGet("/api/payments/events", async (HttpContext context, PaymentCallbackProcessor callbacks, AdminAccess adminAccess) =>
            !adminAccess.IsAuthorized(context.Request) ? Results.Unauthorized() : Results.Ok(await callbacks.ListProcessedEventsAsync(context.RequestAborted)));
    }
}
