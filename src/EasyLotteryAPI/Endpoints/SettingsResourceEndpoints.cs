using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Settings;
using MediatR;

namespace EasyLotteryApi.Endpoints;

/// <summary>
/// Resource-oriented settings API. The old /settings YAML endpoint remains for
/// migration/import, but these endpoints update one settings section at a time.
/// </summary>
internal static class SettingsResourceEndpoints
{
    public static void MapSettingsResourceEndpoints(this WebApplication app)
    {
        Map<ObsLayoutSettings, GetObsLayoutSettingsQuery, UpdateObsLayoutSettingsCommand>(
            app, "obs-layout", (value, etag) => new UpdateObsLayoutSettingsCommand(value, etag), () => new GetObsLayoutSettingsQuery(), allowObsRead: true);
        Map<SoundCueSettings, GetSoundCueSettingsQuery, UpdateSoundCueSettingsCommand>(
            app, "sound-cues", (value, etag) => new UpdateSoundCueSettingsCommand(value, etag), () => new GetSoundCueSettingsQuery(), allowObsRead: true);
        Map<VisualStyleSettings, GetVisualStyleSettingsQuery, UpdateVisualStyleSettingsCommand>(
            app, "visual-style", (value, etag) => new UpdateVisualStyleSettingsCommand(value, etag), () => new GetVisualStyleSettingsQuery(), allowObsRead: true);
        Map<PaymentSettingsResource, GetPaymentSettingsQuery, UpdatePaymentSettingsCommand>(
            app, "payments", (value, etag) => new UpdatePaymentSettingsCommand(value, etag), () => new GetPaymentSettingsQuery());
        Map<OvertimeOverlaySettings, GetOvertimeSettingsQuery, UpdateOvertimeSettingsCommand>(
            app, "overtime", (value, etag) => new UpdateOvertimeSettingsCommand(value, etag), () => new GetOvertimeSettingsQuery(), allowObsRead: true);
    }

    private static void Map<TValue, TQuery, TCommand>(
        WebApplication app,
        string resource,
        Func<TValue, string?, TCommand> commandFactory,
        Func<TQuery> queryFactory,
        bool allowObsRead = false)
        where TQuery : IRequest<SettingsSectionSnapshot<TValue>>
        where TCommand : IRequest<SettingsSectionSnapshot<TValue>>
    {
        app.MapGet($"/api/settings/{resource}", async (HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null && allowObsRead)
            {
                denied = ObsSessionAccess.DeniedResult(access.RequireObsRead(context.Request));
            }
            if (denied is not null) return denied;
            var snapshot = await mediator.Send(queryFactory(), context.RequestAborted);
            return JsonSnapshot(context, snapshot);
        });

        app.MapPut($"/api/settings/{resource}", async (TValue value, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try
            {
                var snapshot = await mediator.Send(commandFactory(value, context.Request.Headers.IfMatch.FirstOrDefault()), context.RequestAborted);
                return JsonSnapshot(context, snapshot);
            }
            catch (ConfigurationConcurrencyException exception)
            {
                return Results.Conflict(new { error = exception.Message, expectedETag = exception.ExpectedETag, actualETag = exception.ActualETag });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting("sensitive");
    }

    private static IResult JsonSnapshot<TValue>(HttpContext context, SettingsSectionSnapshot<TValue> snapshot)
    {
        context.Response.Headers.ETag = snapshot.ETag;
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(snapshot.Value);
    }

}
