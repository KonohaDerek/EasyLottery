using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Viewers;

namespace EasyLotteryCardHub.Endpoints;

public static class ViewerEndpoints
{
    private const string ViewerIdItem = "cardHub.viewerId";

    public static IEndpointRouteBuilder MapViewerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var root = endpoints.MapGroup("/hub/v1/viewers").AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.Pragma = "no-cache";
            context.HttpContext.Response.Headers.Expires = "0";
            try { return await next(context); }
            catch (ViewerServiceException exception)
            {
                return Results.Json(new { error = exception.Code, message_key = exception.MessageKey, details = exception.Details },
                    statusCode: exception.StatusCode);
            }
        });
        var auth = root.MapGroup("/auth").RequireRateLimiting("viewer-auth");
        auth.MapPost("/request-code", async (ViewerCodeRequest request, HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
        {
            var result = await viewers.RequestCodeAsync(request, context.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            return Results.Accepted(value: result);
        });
        auth.MapPost("/verify-code", async (ViewerCodeVerification request, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
            Results.Ok(await viewers.VerifyCodeAsync(request, cancellationToken)));
        auth.MapPost("/refresh", async (ViewerRefreshRequest request, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
            Results.Ok(await viewers.RefreshAsync(request.RefreshToken, cancellationToken)));

        var authenticated = root.MapGroup("/me").RequireRateLimiting("viewer-api").AddEndpointFilter(async (context, next) =>
        {
            var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
            var tokens = context.HttpContext.RequestServices.GetRequiredService<SiteAccessTokenIssuer>();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                || !tokens.TryValidateViewerToken(authorization[7..].Trim(), out var viewerId, out _))
                return Results.Unauthorized();
            context.HttpContext.Items[ViewerIdItem] = viewerId;
            return await next(context);
        });
        authenticated.MapPut("/handle", async (ViewerHandleRequest request, HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
        {
            var handle = await viewers.SetHandleAsync(ReadViewerId(context), request.Handle, cancellationToken);
            return Results.Ok(new { handle = string.IsNullOrEmpty(handle) ? null : handle });
        });
        authenticated.MapGet("/groups", async (HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
            Results.Ok(await viewers.ListGroupsAsync(ReadViewerId(context), cancellationToken)));
        authenticated.MapPost("/groups", async (ViewerGroupRequest request, HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
        {
            var group = await viewers.CreateGroupAsync(ReadViewerId(context), request.StreamerId, request.Name, cancellationToken);
            return Results.Created($"/hub/v1/viewers/me/groups/{group.Id:D}", group);
        });
        authenticated.MapDelete("/groups/{groupId:guid}", async (Guid groupId, HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
        {
            await viewers.DeleteGroupAsync(ReadViewerId(context), groupId, cancellationToken);
            return Results.NoContent();
        });
        authenticated.MapPut("/groups/{groupId:guid}/items/{cardInstanceId:guid}", async (Guid groupId, Guid cardInstanceId, HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
        {
            await viewers.AddCardToGroupAsync(ReadViewerId(context), groupId, cardInstanceId, cancellationToken);
            return Results.NoContent();
        });
        authenticated.MapDelete("/groups/{groupId:guid}/items/{cardInstanceId:guid}", async (Guid groupId, Guid cardInstanceId, HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
        {
            await viewers.RemoveCardFromGroupAsync(ReadViewerId(context), groupId, cardInstanceId, cancellationToken);
            return Results.NoContent();
        });
        authenticated.MapGet("/export", async (HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            return Results.Json(await viewers.ExportAsync(ReadViewerId(context), cancellationToken));
        });
        authenticated.MapDelete("", async (HttpContext context, ViewerAccountsService viewers, CancellationToken cancellationToken) =>
        {
            await viewers.DeleteAccountAsync(ReadViewerId(context), cancellationToken);
            return Results.NoContent();
        });
        return endpoints;
    }

    private static Guid ReadViewerId(HttpContext context) => context.Items.TryGetValue(ViewerIdItem, out var value) && value is Guid viewerId
        ? viewerId : throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_auth_required", "viewer.auth.session.invalid");

    public sealed record ViewerRefreshRequest(string RefreshToken);
    public sealed record ViewerHandleRequest(string? Handle);
    public sealed record ViewerGroupRequest(Guid? StreamerId, string Name);
}
