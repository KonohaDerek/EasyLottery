using System.Text.Json;

namespace EasyLotteryApi.Endpoints;

internal static class PasskeyEndpoints
{
    public static void MapPasskeyEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/passkey/options", async (
            PasskeyOptionsRequest request,
            HttpContext context,
            PasskeyAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await authentication.BeginAsync(context, request, cancellationToken));
            }
            catch (PasskeyAuthenticationException exception)
            {
                return Results.Json(new { error = exception.Message, code = exception.Code }, statusCode: exception.StatusCode);
            }
        }).RequireRateLimiting("authentication");

        app.MapPost("/api/auth/passkey/verify", async (
            PasskeyVerifyRequest request,
            PasskeyAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var issued = await authentication.FinishAsync(request, cancellationToken);
                return Results.Ok(new SessionTokenResponse(issued.Token, issued.ExpiresAtUtc));
            }
            catch (PasskeyAuthenticationException exception)
            {
                return Results.Json(new { error = exception.Message, code = exception.Code }, statusCode: exception.StatusCode);
            }
        }).RequireRateLimiting("authentication");
    }
}
