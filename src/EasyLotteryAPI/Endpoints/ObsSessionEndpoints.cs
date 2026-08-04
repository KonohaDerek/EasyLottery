namespace EasyLotteryApi.Endpoints;

internal static class ObsSessionEndpoints
{
    public static void MapObsSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/session", (AdminLoginRequest request, AdminCredentialService credentials, ObsSessionTokenService tokens) =>
        {
            if (!credentials.Verify(request.Password))
            {
                app.Logger.LogWarning("Admin login rejected from API client.");
                return Results.Unauthorized();
            }

            var issued = tokens.IssueAdminToken();
            return Results.Ok(new SessionTokenResponse(issued.Token, issued.ExpiresAtUtc));
        }).RequireRateLimiting("authentication");

        app.MapPost("/api/obs-sessions", (ObsTokenRequest request, HttpContext context, ObsSessionAccess access, ObsSessionTokenService tokens) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;

            var kind = request.ResourceKind.Trim().ToLowerInvariant();
            if (kind is not ("donate" or "pokebox" or "roulette" or "overtime"))
                return Results.BadRequest(new { error = "不支援的 OBS 資源類型。" });
            if (string.IsNullOrWhiteSpace(request.ResourceId))
                return Results.BadRequest(new { error = "OBS resourceId 不可為空。" });

            var allowedScopes = new HashSet<string>(["read", "control"], StringComparer.Ordinal);
            var scopes = request.Scopes.Select(scope => scope.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();
            if (scopes.Length == 0 || scopes.Any(scope => !allowedScopes.Contains(scope)))
                return Results.BadRequest(new { error = "OBS scope 只能使用 read 或 control。" });

            var issued = tokens.IssueObsToken(kind, request.ResourceId, scopes);
            return Results.Ok(new SessionTokenResponse(issued.Token, issued.ExpiresAtUtc));
        }).RequireRateLimiting("sensitive");

        app.MapDelete("/api/session", (HttpContext context, ObsSessionAccess access, ObsSessionTokenService tokens) =>
        {
            var token = context.Request.Headers[ObsSessionTokenService.HeaderName].ToString();
            if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();
            tokens.Revoke(token);
            return Results.NoContent();
        });

        app.MapGet("/api/session-token", () => Results.NotFound(new { error = "公開 session token endpoint 已停用，請使用管理者登入。" }));
    }
}

public sealed record AdminLoginRequest(string Password);
public sealed record ObsTokenRequest(string ResourceKind, string ResourceId, string[] Scopes);
public sealed record SessionTokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
