using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace EasyLotteryCardHub.Endpoints;

public static class ViewerEndpoints
{
    public static void MapViewerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/viewer/code", async (
            RequestViewerLoginCodeRequest request,
            HttpContext context,
            ViewerAccountsService accounts,
            CancellationToken cancellationToken) =>
        {
            await accounts.RequestLoginCodeAsync(
                request.Email,
                request.TenantId,
                context.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);
            return Results.Json(new { accepted = true }, statusCode: StatusCodes.Status202Accepted);
        }).RequireRateLimiting("authentication");

        app.MapPost("/api/auth/viewer/code/verify", async (
            VerifyViewerLoginCodeRequest request,
            ViewerAccountsService accounts,
            CancellationToken cancellationToken) =>
        {
            var session = await accounts.VerifyLoginCodeAsync(
                request.Email,
                request.LoginCode,
                request.AgeBracket,
                request.TermsAccepted,
                request.TermsVersion,
                cancellationToken);
            return Results.Ok(session);
        }).RequireRateLimiting("authentication");

        app.MapPost("/api/auth/viewer/refresh", async (
            RotateViewerRefreshTokenRequest request,
            ViewerAccountsService accounts,
            CancellationToken cancellationToken) =>
        {
            var session = await accounts.RotateRefreshTokenAsync(request.RefreshToken, cancellationToken);
            return Results.Ok(session);
        }).RequireRateLimiting("authentication");

        var viewers = app.MapGroup("/api/viewer")
            .RequireAuthorization(HubPolicies.Viewer)
            .AddEndpointFilter<ViewerAccountFilter>()
            .RequireRateLimiting("viewer-sensitive");

        viewers.MapGet("/groups", async (HttpContext context, ViewerGroupsService groups, CancellationToken cancellationToken) =>
            Results.Ok(await groups.ListAsync(ViewerEndpointIdentity.GetViewerId(context), cancellationToken)));

        viewers.MapPost("/groups", async (
            CreateViewerGroupRequest request,
            HttpContext context,
            ViewerGroupsService groups,
            CancellationToken cancellationToken) =>
        {
            var group = await groups.CreateAsync(ViewerEndpointIdentity.GetViewerId(context), request.Name, cancellationToken);
            return Results.Created($"/api/viewer/groups/{group.Id:D}", group);
        });

        viewers.MapPut("/groups/{groupId:guid}", async (
            Guid groupId,
            UpdateViewerGroupRequest request,
            HttpContext context,
            ViewerGroupsService groups,
            CancellationToken cancellationToken) =>
        {
            var group = await groups.UpdateAsync(ViewerEndpointIdentity.GetViewerId(context), groupId, request.Name, cancellationToken);
            return group is null
                ? Results.NotFound(new HubApiError("viewer_group_not_found", "The group was not found."))
                : Results.Ok(group);
        });

        viewers.MapDelete("/groups/{groupId:guid}", async (
            Guid groupId,
            HttpContext context,
            ViewerGroupsService groups,
            CancellationToken cancellationToken) =>
        {
            var deleted = await groups.DeleteAsync(ViewerEndpointIdentity.GetViewerId(context), groupId, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound(new HubApiError("viewer_group_not_found", "The group was not found."));
        });

        viewers.MapGet("/account/export", async (
            HttpContext context,
            ViewerAccountsService accounts,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            var export = await accounts.ExportAsync(ViewerEndpointIdentity.GetViewerId(context), cancellationToken);
            return Results.Json(export);
        });

        viewers.MapDelete("/account", async (
            HttpContext context,
            ViewerAccountsService accounts,
            CancellationToken cancellationToken) =>
        {
            await accounts.DeleteAccountAsync(ViewerEndpointIdentity.GetViewerId(context), cancellationToken);
            return Results.NoContent();
        });
    }
}

public sealed record RequestViewerLoginCodeRequest(string? Email, Guid? TenantId = null);
public sealed record VerifyViewerLoginCodeRequest(
    string? Email,
    string? LoginCode,
    string? AgeBracket = null,
    bool TermsAccepted = false,
    string? TermsVersion = null);
public sealed record RotateViewerRefreshTokenRequest(string? RefreshToken);
public sealed record CreateViewerGroupRequest(string? Name);
public sealed record UpdateViewerGroupRequest(string? Name);
