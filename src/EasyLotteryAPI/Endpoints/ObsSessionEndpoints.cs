namespace EasyLotteryApi.Endpoints;

internal static class ObsSessionEndpoints
{
    public static void MapObsSessionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/session-token", (ObsSessionTokenService tokens) =>
        {
            var issued = tokens.IssueToken();
            return Results.Ok(new SessionTokenResponse(issued.Token, issued.ExpiresAtUtc));
        });
    }
}

public sealed record SessionTokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
