using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Interactions;
using EasyLotteryDomain.Services;
using EasyLotteryApi.Security;

namespace EasyLotteryApi.Interactions;

public sealed record CreateInteractionRoundRequest(string Name, string Type, string CommandPrefix, string[]? VoteOptions, string? CorrectAnswer, int CorrectAnswerPoints = 0, int EligibilityTickets = 0);
public sealed record InteractionRoundResource(Guid Id, string Name, string Status, string Type, string CommandPrefix, IReadOnlyList<string> VoteOptions, int CorrectAnswerPoints, int EligibilityTickets);
public sealed record HostPointAdjustmentRequest(Guid AudienceProfileId, int PointDelta, string Reason);
public sealed record AudienceInteractionRequest(Guid AudienceProfileId, string Platform, string ChannelScope, string ExternalUserId, string ExternalEventId, string Content);

public sealed class InteractionRoundService(IInteractionsYamlDocumentRepository repository, InteractionStateBroadcaster broadcaster)
{
    private readonly InteractionRoundEngine _engine = new();
    public async Task<InteractionRoundResource> CreateAsync(CreateInteractionRoundRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InteractionRoundType>(request.Type, true, out var type)) throw new InvalidOperationException("不支援的回合類型。");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.CommandPrefix)) throw new InvalidOperationException("名稱與指令不可空白。");
        var document = await repository.ReadAsync(cancellationToken);
        var round = new InteractionRound { Name = request.Name.Trim(), Type = type, CommandPrefix = request.CommandPrefix.Trim(), VoteOptions = request.VoteOptions?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [], CorrectAnswer = request.CorrectAnswer?.Trim() ?? "", CorrectAnswerPoints = request.CorrectAnswerPoints, EligibilityTickets = request.EligibilityTickets };
        document.Rounds.Add(round);
        await repository.SaveAsync(document, cancellationToken);
        await broadcaster.PublishAsync(round.Id, cancellationToken);
        return ToResource(round);
    }

    public async Task<InteractionRoundResource> TransitionAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        var document = await repository.ReadAsync(cancellationToken);
        var round = document.Rounds.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("找不到互動回合。");
        round.Status = (round.Status, action.ToLowerInvariant()) switch
        {
            (InteractionRoundStatus.Draft or InteractionRoundStatus.Scheduled, "start") => InteractionRoundStatus.Live,
            (InteractionRoundStatus.Live, "pause") => InteractionRoundStatus.Paused,
            (InteractionRoundStatus.Live or InteractionRoundStatus.Paused, "settle") => InteractionRoundStatus.Settled,
            (InteractionRoundStatus.Draft or InteractionRoundStatus.Scheduled or InteractionRoundStatus.Live or InteractionRoundStatus.Paused, "cancel") => InteractionRoundStatus.Cancelled,
            _ => throw new InvalidOperationException("此回合無法執行該狀態變更。")
        };
        await repository.SaveAsync(document, cancellationToken);
        await broadcaster.PublishAsync(round.Id, cancellationToken);
        return ToResource(round);
    }

    public async Task<IReadOnlyList<InteractionRoundResource>> ListAsync(CancellationToken cancellationToken) =>
        (await repository.ReadAsync(cancellationToken)).Rounds.Select(ToResource).ToList();

    public async Task AdjustPointsAsync(Guid id, HostPointAdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (request.AudienceProfileId == Guid.Empty || request.PointDelta == 0 || string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("調整對象、分數與原因皆為必填。");
        var document = await repository.ReadAsync(cancellationToken);
        var round = document.Rounds.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("找不到互動回合。");
        var points = round.State.PointsByProfile.ToDictionary(item => item.Key, item => item.Value);
        points[request.AudienceProfileId] = points.GetValueOrDefault(request.AudienceProfileId) + request.PointDelta;
        round.State = round.State with { PointsByProfile = points };
        round.HostAdjustments.Add(new InteractionHostAdjustment { AudienceProfileId = request.AudienceProfileId, PointDelta = request.PointDelta, Reason = request.Reason.Trim() });
        await repository.SaveAsync(document, cancellationToken);
        await broadcaster.PublishAsync(round.Id, cancellationToken);
    }

    public async Task<InteractionDecision> ProcessAudienceEventAsync(Guid id, AudienceInteractionRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PlatformKind>(request.Platform, true, out var platform)) throw new InvalidOperationException("不支援的互動平台。");
        var document = await repository.ReadAsync(cancellationToken);
        var round = document.Rounds.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("找不到互動回合。");
        var profile = document.AudienceProfiles.SingleOrDefault(item => item.Id == request.AudienceProfileId) ?? new AudienceProfile { Id = request.AudienceProfileId, DisplayName = request.ExternalUserId };
        if (!document.AudienceProfiles.Any(item => item.Id == profile.Id)) document.AudienceProfiles.Add(profile);
        var identity = document.PlatformIdentities.SingleOrDefault(item => item.Platform == platform && item.ChannelScope == request.ChannelScope && item.ExternalUserId == request.ExternalUserId)
            ?? new PlatformIdentity(platform, request.ExternalUserId, request.ChannelScope, profile.Id, profile.DisplayName);
        if (!document.PlatformIdentities.Any(item => item.Id == identity.Id)) document.PlatformIdentities.Add(identity);
        var interactionEvent = new InteractionEvent(platform, request.ChannelScope, request.ExternalEventId, request.ExternalUserId, request.Content);
        var decision = _engine.Apply(round, round.State, profile.Id, interactionEvent);
        if (decision.Accepted) round.State = decision.NextState;
        await repository.SaveAsync(document, cancellationToken);
        if (decision.Accepted) await broadcaster.PublishAsync(round.Id, cancellationToken);
        return decision;
    }

    private static InteractionRoundResource ToResource(InteractionRound round) => new(round.Id, round.Name, round.Status.ToString(), round.Type.ToString(), round.CommandPrefix, round.VoteOptions, round.CorrectAnswerPoints, round.EligibilityTickets);
}

internal static class InteractionRoundEndpoints
{
    public static void MapInteractionRoundEndpoints(this WebApplication app)
    {
        app.MapGet("/api/interactions/rounds", async (HttpContext context, InteractionRoundService rounds, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            return denied ?? Results.Ok(await rounds.ListAsync(context.RequestAborted));
        });
        app.MapPost("/api/interactions/rounds", async (CreateInteractionRoundRequest request, HttpContext context, InteractionRoundService rounds, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try { return Results.Created("/api/interactions/rounds", await rounds.CreateAsync(request, context.RequestAborted)); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting("sensitive");
        app.MapPost("/api/interactions/rounds/{id:guid}/{action}", async (Guid id, string action, HttpContext context, InteractionRoundService rounds, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try { return Results.Ok(await rounds.TransitionAsync(id, action, context.RequestAborted)); }
            catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting("sensitive");
        app.MapPost("/api/interactions/rounds/{id:guid}/points", async (Guid id, HostPointAdjustmentRequest request, HttpContext context, InteractionRoundService rounds, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try { await rounds.AdjustPointsAsync(id, request, context.RequestAborted); return Results.NoContent(); }
            catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting("sensitive");
        app.MapPost("/api/interactions/rounds/{id:guid}/events", async (Guid id, AudienceInteractionRequest request, HttpContext context, InteractionRoundService rounds, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try { return Results.Ok(await rounds.ProcessAudienceEventAsync(id, request, context.RequestAborted)); }
            catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting("sensitive");
    }
}
