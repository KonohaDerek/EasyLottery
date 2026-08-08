using System.Text.Json;
using EasyLotteryApplication.ObsAssets;
using EasyLotteryDomain.Models.Obs;
using EasyLotteryDomain.Services;
using MediatR;

namespace EasyLotteryApi.Endpoints;

internal static class ObsAssetEndpoints
{
    public static void MapObsAssetEndpoints(this WebApplication app)
    {
        app.MapGet("/api/obs-assets", async (HttpContext context, IMediator mediator, IEasyLotteryConfigStore configStore, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var assets = await mediator.Send(new ListObsAssetsQuery(), context.RequestAborted);
            return Results.Ok(await AddConfiguredReferencesAsync(assets, configStore, context.RequestAborted));
        });

        app.MapGet("/api/obs-assets/unused", async (HttpContext context, IObsAssetRepository repository, IEasyLotteryConfigStore configStore, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var assets = await AddConfiguredReferencesAsync(await repository.ListAsync(context.RequestAborted), configStore, context.RequestAborted);
            return Results.Ok(assets.Where(asset => asset.ReferencedBy.Count == 0).ToList());
        });

        app.MapGet("/api/obs-assets/export", async (HttpContext context, IObsAssetRepository repository, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var package = await repository.ExportPackageAsync(context.RequestAborted);
            return Results.File(package, "application/zip", $"obs-assets-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        });

        app.MapPost("/api/obs-assets/import", async (HttpContext context, IObsAssetRepository repository, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try
            {
                var imported = await repository.ImportPackageAsync(context.Request.Body, context.RequestAborted);
                return Results.Ok(imported);
            }
            catch (InvalidDataException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/obs-assets/{id:guid}", async (Guid id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var asset = await mediator.Send(new GetObsAssetQuery(id), context.RequestAborted);
            return asset is null ? Results.NotFound() : Results.Ok(asset);
        });

        // OBS image/audio tags cannot attach a custom header. Asset IDs are random GUIDs,
        // so content URLs are intentionally read-only and suitable for local OBS sources.
        app.MapGet("/api/obs-assets/{id:guid}/content", async (Guid id, IObsAssetRepository repository, CancellationToken cancellationToken) =>
        {
            var asset = await repository.GetAsync(id, cancellationToken);
            if (asset is null) return Results.NotFound();
            var stream = await repository.OpenReadAsync(id, cancellationToken);
            return stream is null
                ? Results.NotFound()
                : Results.Stream(stream, asset.ContentType, asset.FileName, enableRangeProcessing: true);
        });

        app.MapPost("/api/obs-assets", async (HttpContext context, IMediator mediator, ObsSessionAccess access, IConfiguration configuration) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            if (!context.Request.HasFormContentType) return Results.BadRequest(new { error = "資產上傳必須使用 multipart/form-data。" });

            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null) return Results.BadRequest(new { error = "找不到上傳的資產檔案。" });
            if (!Enum.TryParse<ObsAssetKind>(form["kind"].ToString(), ignoreCase: true, out var kind)) kind = InferKind(file.ContentType);
            var references = form["referencedBy"].ToString().Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var maxBytes = ReadMaxAssetBytes(configuration);
            if (file.Length > maxBytes) return Results.BadRequest(new { error = $"資產檔案不可超過 {maxBytes / 1024 / 1024} MB。" });
            try
            {
                await using var stream = file.OpenReadStream();
                var saved = await mediator.Send(new UploadObsAssetCommand(file.FileName, file.ContentType, kind, stream, file.Length, references), context.RequestAborted);
                return Results.Created($"/api/obs-assets/{saved.Id:D}", saved);
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting("sensitive");

        app.MapDelete("/api/obs-assets/{id:guid}", async (Guid id, HttpContext context, IMediator mediator, IObsAssetRepository repository, IEasyLotteryConfigStore configStore, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try
            {
                var asset = (await AddConfiguredReferencesAsync(await repository.ListAsync(context.RequestAborted), configStore, context.RequestAborted))
                    .FirstOrDefault(item => item.Id == id);
                if (asset?.ReferencedBy.Count > 0)
                    return Results.Conflict(new { error = "資產仍被使用中，請先移除活動或模板引用後再刪除。" });
                return await mediator.Send(new DeleteObsAssetCommand(id), context.RequestAborted)
                    ? Results.NoContent()
                    : Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        }).RequireRateLimiting("sensitive");
    }

    private static async Task<List<ObsAsset>> AddConfiguredReferencesAsync(
        IReadOnlyList<ObsAsset> assets,
        IEasyLotteryConfigStore configStore,
        CancellationToken cancellationToken)
    {
        var document = await configStore.LoadAsync(cancellationToken);
        var configuration = JsonSerializer.Serialize(document);
        foreach (var asset in assets)
        {
            if (configuration.Contains(asset.Id.ToString("D"), StringComparison.OrdinalIgnoreCase)
                && !asset.ReferencedBy.Contains("config:活動或模板", StringComparer.OrdinalIgnoreCase))
            {
                asset.ReferencedBy.Add("config:活動或模板");
            }
        }
        return assets.ToList();
    }

    private static ObsAssetKind InferKind(string? contentType) =>
        contentType?.ToLowerInvariant() switch
        {
            var value when value?.StartsWith("image/", StringComparison.Ordinal) == true => ObsAssetKind.Image,
            var value when value?.StartsWith("audio/", StringComparison.Ordinal) == true => ObsAssetKind.Audio,
            "video/webm" => ObsAssetKind.Video,
            _ => ObsAssetKind.Other
        };

    private static long ReadMaxAssetBytes(IConfiguration configuration) =>
        long.TryParse(configuration["Storage:MaxAssetBytes"], out var configured)
            ? Math.Clamp(configured, 64 * 1024, 50 * 1024 * 1024)
            : 10 * 1024 * 1024;
}
