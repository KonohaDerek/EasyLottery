using EasyLotteryCardHub.Invites;

namespace EasyLotteryCardHub.Endpoints;

public static class StreamerPasskeyLoginEndpoints
{
    public static IEndpointRouteBuilder MapStreamerPasskeyLoginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/hub/v1/streamers/passkey").RequireRateLimiting("invite-registration").AddEndpointFilter(async (context, next) =>
        {
            var environment = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var ip = context.HttpContext.Connection.RemoteIpAddress;
            if (!context.HttpContext.Request.IsHttps && !(environment.IsDevelopment() && (ip is null || System.Net.IPAddress.IsLoopback(ip))))
                return Results.BadRequest(new { error = "https_required" });
            return await next(context);
        });

        group.MapPost("/options", async (StreamerPasskeyLoginService login, CancellationToken cancellationToken) =>
        {
            var options = await login.CreateOptionsAsync(cancellationToken);
            return Results.Ok(new { challenge_id = options.ChallengeId, options = options.Options, expires_at_utc = options.ExpiresAtUtc });
        });

        group.MapPost("/verify", async (LoginVerifyRequest request, StreamerPasskeyLoginService login, CancellationToken cancellationToken) =>
        {
            var result = await login.VerifyAsync(request.ChallengeId, request.Credential, cancellationToken);
            return result is null
                ? Results.Unauthorized()
                : Results.Ok(new { tenant_id = result.TenantId, account_id = result.AccountId, access_token = result.AccessToken, expires_in = result.ExpiresInSeconds });
        });
        return endpoints;
    }

    public sealed record LoginVerifyRequest(Guid ChallengeId, System.Text.Json.JsonElement Credential);
}
