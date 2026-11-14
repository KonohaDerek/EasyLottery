using EasyLotteryCardHub.Invites;

namespace EasyLotteryCardHub.Endpoints;

public static class InviteEndpoints
{
    public static RouteGroupBuilder MapInviteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/hub/v1/invites").RequireRateLimiting("invite-registration").AddEndpointFilter(async (context, next) =>
        {
            var environment = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var ip = context.HttpContext.Connection.RemoteIpAddress;
            if (!context.HttpContext.Request.IsHttps && !(environment.IsDevelopment() && (ip is null || System.Net.IPAddress.IsLoopback(ip))))
                return Results.BadRequest(new { error = "https_required" });
            return await next(context);
        });

        group.MapPost("/{token}/passkey/options", async (string token, CardHubInviteService invitations, CancellationToken cancellationToken) =>
        {
            var result = await invitations.GetRegistrationOptionsAsync(token, cancellationToken);
            return result is null
                ? Results.Json(new { error = "invite_invalid_or_expired" }, statusCode: StatusCodes.Status410Gone)
                : Results.Ok(result);
        });

        group.MapPost("/{token}/passkey/verify", async (string token, PasskeyRegistrationRequest request, CardHubInviteService invitations, CancellationToken cancellationToken) =>
        {
            var result = await invitations.CompleteRegistrationAsync(token, request.Credential, cancellationToken);
            return result is null
                ? Results.BadRequest(new { error = "passkey_registration_failed" })
                : Results.Created("/hub/v1/streamer/sites", result);
        });
        return group;
    }

    public sealed record PasskeyRegistrationRequest(System.Text.Json.JsonElement Credential);
}
