using EasyLotteryApi.Security;
using EasyLotteryApplication.Interactions;

namespace EasyLotteryApi.Interactions;

public sealed record IssueIdentityLinkRequest(Guid SourceIdentityId, Guid TargetIdentityId);

internal static class IdentityLinkEndpoints
{
    public static void MapIdentityLinkEndpoints(this WebApplication app)
    {
        app.MapPost("/api/interactions/identity-links", async (IssueIdentityLinkRequest request, HttpContext context, IdentityLinkService links, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try { return Results.Ok(await links.IssueAsync(request.SourceIdentityId, request.TargetIdentityId, context.RequestAborted)); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/interactions/identity-links/complete", async (string? code, HttpContext context, IdentityLinkService links) =>
        {
            try { await links.CompleteAsync(code ?? "", context.RequestAborted); return Results.NoContent(); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting("sensitive");
    }
}
