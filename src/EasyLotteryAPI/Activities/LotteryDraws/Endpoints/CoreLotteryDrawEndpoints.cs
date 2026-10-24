using EasyLotteryApi.Security;
using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Settings;
using Results = Microsoft.AspNetCore.Http.Results;

namespace EasyLotteryApi.Activities.LotteryDraws.Endpoints;

internal static class CoreLotteryDrawEndpoints
{
    public static void MapCoreLotteryDrawEndpoints(this WebApplication app)
    {
        app.MapGet("/api/lottery-draw-records", async (
            HttpContext context,
            int? page,
            int? pageSize,
            string? search,
            DateTimeOffset? fromUtc,
            DateTimeOffset? beforeUtc,
            CoreLotteryDrawHistoryRepository repository,
            ObsSessionAccess sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;

            var pageNumber = page ?? 1;
            var resultPageSize = pageSize ?? 20;
            var searchText = search?.Trim() ?? "";
            if (pageNumber < 1 || resultPageSize is < 1 or > 100 || searchText.Length > 120
                || (fromUtc.HasValue && beforeUtc.HasValue && fromUtc.Value > beforeUtc.Value))
                return Results.BadRequest(new { error = "查詢條件或分頁參數無效。" });

            var records = await repository.LoadAllAsync(context.RequestAborted);
            var query = records.AsEnumerable();
            if (fromUtc.HasValue) query = query.Where(record => record.DrawnAtUtc >= fromUtc.Value);
            if (beforeUtc.HasValue) query = query.Where(record => record.DrawnAtUtc < beforeUtc.Value);
            if (searchText.Length > 0)
            {
                query = query.Where(record =>
                    record.WinnerDisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || record.PrizeName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = query.OrderByDescending(record => record.DrawnAtUtc)
                .ThenByDescending(record => record.DrawId);
            var totalCount = ordered.Count();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)resultPageSize);
            var offset = (long)(pageNumber - 1) * resultPageSize;
            var items = ordered.Skip((int)Math.Min(offset, totalCount)).Take(resultPageSize)
                .Select(record => new CoreLotteryDrawSummary(
                    record.DrawId,
                    record.DrawnAtUtc,
                    record.WinnerParticipantId,
                    record.WinnerDisplayName,
                    record.PrizeName,
                    record.Participants?.Count ?? 0,
                    record.RuleSnapshot.EligibleEntryCount))
                .ToArray();
            return Results.Ok(new CoreLotteryDrawHistoryPage(items, pageNumber, resultPageSize, totalCount, totalPages));
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/lottery-draw-records/{drawId:guid}", async (
            Guid drawId,
            HttpContext context,
            CoreLotteryDrawHistoryRepository repository,
            ObsSessionAccess sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var record = await repository.FindAsync(drawId, context.RequestAborted);
            return record is null ? Results.NotFound(new { error = "尚未找到此抽獎紀錄。" }) : Results.Ok(ToSummary(record));
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/lottery-draw-records/{drawId:guid}/summary", async (
            Guid drawId,
            HttpContext context,
            CoreLotteryDrawHistoryRepository repository,
            ObsSessionAccess sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var record = await repository.FindAsync(drawId, context.RequestAborted);
            return record is null ? Results.NotFound(new { error = "尚未找到此抽獎紀錄。" }) : Results.Ok(ToSummary(record));
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/lottery-draw-records/{drawId:guid}/participants", async (
            Guid drawId,
            HttpContext context,
            int? page,
            int? pageSize,
            CoreLotteryDrawHistoryRepository repository,
            ObsSessionAccess sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var pageNumber = page ?? 1;
            var resultPageSize = pageSize ?? 50;
            if (pageNumber < 1 || resultPageSize is < 1 or > 100)
                return Results.BadRequest(new { error = "查詢條件或分頁參數無效。" });
            var record = await repository.FindAsync(drawId, context.RequestAborted);
            if (record is null) return Results.NotFound(new { error = "尚未找到此抽獎紀錄。" });
            var entries = record.Participants ?? [];
            var totalCount = entries.Count;
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)resultPageSize);
            var offset = (long)(pageNumber - 1) * resultPageSize;
            var items = entries.Skip((int)Math.Min(offset, totalCount)).Take(resultPageSize).ToArray();
            return Results.Ok(new CoreLotteryDrawParticipantPage(items, pageNumber, resultPageSize, totalCount, totalPages));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/lottery-draw-records", async (
            RecordCoreLotteryDrawRequest request,
            HttpContext context,
            CoreLotteryDrawHistoryRepository repository,
            ObsSessionAccess sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var validationError = Validate(request);
            if (validationError is not null) return Results.BadRequest(new { error = validationError });

            try
            {
                var saved = await repository.SaveAsync(request.DrawId, request.WinnerParticipantId,
                    request.RuleSnapshot, request.Participants, context.RequestAborted);
                return saved.IsNew
                    ? Results.Created($"/api/lottery-draw-records/{saved.Record.DrawId:D}", ToSummary(saved.Record))
                    : Results.Ok(ToSummary(saved.Record));
            }
            catch (CoreLotteryDrawIdConflictException)
            {
                return Results.Conflict(new { error = "此抽獎識別碼已對應不同結果，請先查詢紀錄對帳。" });
            }
        }).RequireRateLimiting("sensitive");
    }

    private static CoreLotteryDrawSummary ToSummary(CoreLotteryDrawRecord record) => new(
        record.DrawId,
        record.DrawnAtUtc,
        record.WinnerParticipantId,
        record.WinnerDisplayName,
        record.PrizeName,
        record.Participants?.Count ?? 0,
        record.RuleSnapshot.EligibleEntryCount,
        record.RuleSnapshot);

    private static string? Validate(RecordCoreLotteryDrawRequest request)
    {
        if (request.DrawId == Guid.Empty || string.IsNullOrWhiteSpace(request.WinnerParticipantId)
            || request.RuleSnapshot is null || request.Participants is null || request.Participants.Count == 0)
            return "抽獎識別碼、得獎籤、規則與完整名單不可空白。";

        var rules = request.RuleSnapshot;
        if (rules.AlgorithmVersion != "secure-random-index-v1"
            || rules.EligibilityPolicyVersion != "not-previously-won-v1"
            || rules.GroupWinnerPolicyVersion != "exclude-lottery-group-after-win-v1"
            || rules.PrizeSelectionPolicyVersion != "first-remaining-prize-v1"
            || rules.EligibleEntryCount < 1 || rules.PrizeQueue is null || rules.PrizeQueue.Count == 0
            || rules.PrizeQueue.Any(prize => string.IsNullOrWhiteSpace(prize) || prize.Length > 120))
            return "抽獎規則快照無效。";

        var participantIds = new HashSet<string>(StringComparer.Ordinal);
        var eligibleCount = 0;
        CoreLotteryDrawParticipantSnapshot? winner = null;
        foreach (var participant in request.Participants)
        {
            if (participant is null || string.IsNullOrWhiteSpace(participant.ParticipantId) || participant.ParticipantId.Length > 256
                || !participantIds.Add(participant.ParticipantId)
                || string.IsNullOrWhiteSpace(participant.LotteryGroupId) || participant.LotteryGroupId.Length > 256
                || string.IsNullOrWhiteSpace(participant.DisplayName) || participant.DisplayName.Length > 160
                || participant.Level is null || participant.Level.Length > 160
                || participant.Group is null || participant.Group.Length > 160
                || participant.Tags is null || participant.Tags.Any(tag => tag is null || tag.Length > 160)
                || participant.EligibilityStatus is not ("eligible" or "already-won"))
                return "抽獎名單快照包含無效資料。";

            if (participant.ExternalIdentity is { } identity
                && (string.IsNullOrWhiteSpace(identity.Platform) || identity.Platform.Length > 80
                    || string.IsNullOrWhiteSpace(identity.AccountId) || identity.AccountId.Length > 512))
                return "外部帳號識別格式無效。";

            if (participant.EligibilityStatus == "eligible") eligibleCount++;
            if (participant.ParticipantId == request.WinnerParticipantId) winner = participant;
        }

        if (eligibleCount != rules.EligibleEntryCount || winner is null || winner.EligibilityStatus != "eligible")
            return "合格籤數或得獎籤與完整名單快照不一致。";

        return null;
    }
}

internal sealed record RecordCoreLotteryDrawRequest(
    Guid DrawId,
    string WinnerParticipantId,
    CoreLotteryDrawRuleSnapshot RuleSnapshot,
    List<CoreLotteryDrawParticipantSnapshot> Participants);

internal sealed record CoreLotteryDrawSummary(
    Guid DrawId,
    DateTimeOffset DrawnAtUtc,
    string WinnerParticipantId,
    string WinnerDisplayName,
    string PrizeName,
    int ParticipantCount,
    int EligibleEntryCount,
    CoreLotteryDrawRuleSnapshot? RuleSnapshot = null);

internal sealed record CoreLotteryDrawParticipantPage(
    IReadOnlyList<CoreLotteryDrawParticipantSnapshot> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

internal sealed record CoreLotteryDrawHistoryPage(
    IReadOnlyList<CoreLotteryDrawSummary> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
