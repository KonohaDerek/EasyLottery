using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EasyLotteryApi.Interactions;
using EasyLotteryApplication.Interactions;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

/// <summary>Issue #305: interaction eligibility snapshot import into the core lottery and immutable draw provenance.</summary>
[TestClass]
public sealed class CoreLotteryInteractionEligibilityTests
{
    private string _directory = "";
    private WebApplicationFactory<Program> _factory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-305-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Storage:Directory", _directory);
            builder.UseSetting("Testing:EnableAdminSessionToken", "true");
            builder.UseSetting("Testing:SensitivePermitLimit", "600");
        });
    }

    [TestCleanup]
    public void Cleanup()
    {
        _factory.Dispose();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [TestMethod]
    public async Task PreviewAndImport_RequireAdminSession()
    {
        var roundId = await CreateRoundAsync("auth");
        using var anonymous = _factory.CreateClient();

        var preview = await anonymous.GetAsync($"/api/interactions/rounds/{roundId:D}/eligibility-preview");
        var import = await anonymous.PostAsync($"/api/interactions/rounds/{roundId:D}/eligibility-import", null);

        CollectionAssert.Contains(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }, preview.StatusCode);
        CollectionAssert.Contains(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }, import.StatusCode);
        var round = (await Interactions.ReadAsync()).Rounds.Single(item => item.Id == roundId);
        Assert.IsNull(round.EligibilitySnapshot);
    }

    [TestMethod]
    public async Task PreviewAndImport_MissingRound_ReturnNotFound()
    {
        using var client = await AdminClientAsync();
        var missing = Guid.NewGuid();

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/api/interactions/rounds/{missing:D}/eligibility-preview")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.PostAsync($"/api/interactions/rounds/{missing:D}/eligibility-import", null)).StatusCode);
    }

    [TestMethod]
    public async Task Import_EmptyRound_IsRejectedWithoutPersistingSnapshot()
    {
        var roundId = await CreateRoundAsync("empty");
        using var client = await AdminClientAsync();

        var response = await client.PostAsync($"/api/interactions/rounds/{roundId:D}/eligibility-import", null);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        var preview = await Snapshots.PreviewAsync(roundId);
        Assert.IsFalse(preview.Imported);
        Assert.IsNull(preview.SnapshotId);
        Assert.AreEqual(0, preview.Entries!.Count);
    }

    [TestMethod]
    public async Task Import_CancelledRound_IsRejected()
    {
        var roundId = await CreateRoundAsync("cancelled");
        var profile = await CreateProfileAsync(roundId, "YouTube", "scope", "UC-cancelled");
        await Rounds.AdjustPointsAsync(roundId, new HostPointAdjustmentRequest(profile, 1, "eligible"), CancellationToken.None);
        await Rounds.TransitionAsync(roundId, "cancel", CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Snapshots.ImportAsync(roundId));
        Assert.IsFalse((await Snapshots.PreviewAsync(roundId)).Imported);
    }

    [TestMethod]
    public async Task Preview_ProjectsRoundAndStableProfileIdentities_KeepsSameNameProfilesDistinct()
    {
        var roundId = await CreateRoundAsync("same-name");
        var youtube = await CreateProfileAsync(roundId, "YouTube", "scope", "same-name");
        var twitch = await CreateProfileAsync(roundId, "Twitch", "scope", "same-name");
        await Rounds.AdjustPointsAsync(roundId, new HostPointAdjustmentRequest(youtube, 1, "eligible"), CancellationToken.None);
        await Rounds.AdjustPointsAsync(roundId, new HostPointAdjustmentRequest(twitch, 1, "eligible"), CancellationToken.None);

        using var client = await AdminClientAsync();
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/interactions/rounds/{roundId:D}/eligibility-preview");

        Assert.AreEqual("same-name round", json.GetProperty("roundName").GetString());
        Assert.AreEqual("Draft", json.GetProperty("roundStatus").GetString());
        Assert.IsTrue(!json.TryGetProperty("snapshotId", out var snapshotId) || snapshotId.ValueKind == JsonValueKind.Null);
        Assert.IsFalse(json.GetProperty("imported").GetBoolean());
        var entries = json.GetProperty("entries").EnumerateArray().ToList();
        Assert.AreEqual(2, entries.Count);
        CollectionAssert.AreEquivalent(new[] { youtube, twitch }, entries.Select(entry => entry.GetProperty("audienceProfileId").GetGuid()).ToArray());
        Assert.IsTrue(entries.All(entry => entry.GetProperty("displayName").GetString() == "same-name"));
        CollectionAssert.AreEquivalent(new[] { "youtube", "twitch" },
            entries.Select(entry => entry.GetProperty("identities")[0].GetProperty("platform").GetString()).ToArray());
        Assert.IsFalse(json.GetRawText().Contains("accessToken", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.GetRawText().Contains("content", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Preview_ProjectsChannelScope_SameAccountIdUnderDifferentScopesStaysDistinct()
    {
        var roundId = await CreateRoundAsync("scoped");
        var scopeA = await CreateProfileAsync(roundId, "YouTube", "channel-a", "UC-shared");
        var scopeB = await CreateProfileAsync(roundId, "YouTube", "channel-b", "UC-shared");
        await Rounds.AdjustPointsAsync(roundId, new HostPointAdjustmentRequest(scopeA, 1, "eligible"), CancellationToken.None);
        await Rounds.AdjustPointsAsync(roundId, new HostPointAdjustmentRequest(scopeB, 1, "eligible"), CancellationToken.None);

        using var client = await AdminClientAsync();
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/interactions/rounds/{roundId:D}/eligibility-preview");

        var entries = json.GetProperty("entries").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("audienceProfileId").GetGuid(), entry => entry.GetProperty("identities").EnumerateArray().ToList());
        Assert.AreEqual(2, entries.Count, "Same account ID under different channel scopes must not collapse into one profile.");
        Assert.AreEqual(1, entries[scopeA].Count);
        Assert.AreEqual(1, entries[scopeB].Count);
        Assert.AreEqual("channel-a", entries[scopeA][0].GetProperty("channelScope").GetString());
        Assert.AreEqual("channel-b", entries[scopeB][0].GetProperty("channelScope").GetString());
        Assert.IsTrue(entries.Values.All(identities => identities[0].GetProperty("accountId").GetString() == "UC-shared"
            && identities[0].GetProperty("platform").GetString() == "youtube"));
    }

    [TestMethod]
    public async Task RepeatedImport_IsIdempotentAndSnapshotSurvivesNewEligibilityMergeAndUnlink()
    {
        var roundId = await CreateRoundAsync("stable");
        var first = await CreateProfileAsync(roundId, "YouTube", "scope", "UC-first");
        var second = await CreateProfileAsync(roundId, "YouTube", "scope", "UC-second");
        await Rounds.AdjustPointsAsync(roundId, new HostPointAdjustmentRequest(first, 1, "eligible"), CancellationToken.None);
        await Rounds.AdjustPointsAsync(roundId, new HostPointAdjustmentRequest(second, 1, "eligible"), CancellationToken.None);

        using var client = await AdminClientAsync();
        var importedResponse = await client.PostAsync($"/api/interactions/rounds/{roundId:D}/eligibility-import", null);
        Assert.AreEqual(HttpStatusCode.OK, importedResponse.StatusCode);
        var imported = await Snapshots.ImportAsync(roundId);
        var importedJson = await importedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(imported.SnapshotId, importedJson.GetProperty("snapshotId").GetGuid());
        CollectionAssert.AreEqual(new[] { first, second }, imported.Entries!.Select(entry => entry.AudienceProfileId).ToArray());

        // New eligibility after import must not change the imported snapshot.
        var late = await CreateProfileAsync(roundId, "YouTube", "scope", "UC-late");
        await Rounds.AdjustPointsAsync(roundId, new HostPointAdjustmentRequest(late, 1, "eligible"), CancellationToken.None);
        // Merge first into second, then unlink second's original identity.
        var document = await Interactions.ReadAsync();
        var firstIdentity = document.PlatformIdentities.Single(identity => identity.AudienceProfileId == first);
        var secondIdentity = document.PlatformIdentities.Single(identity => identity.AudienceProfileId == second);
        var link = await Links.IssueAsync(firstIdentity.Id, secondIdentity.Id);
        await Links.CompleteAsync(link.Code);
        await Links.UnlinkAsync(secondIdentity.Id, "test unlink");

        var retried = await Snapshots.ImportAsync(roundId);
        var preview = await Snapshots.PreviewAsync(roundId);

        Assert.AreEqual(imported.SnapshotId, retried.SnapshotId);
        Assert.AreEqual(imported.SnapshotCreatedAtUtc, retried.SnapshotCreatedAtUtc);
        CollectionAssert.AreEqual(new[] { first, second }, retried.ProfileIds.ToArray());
        CollectionAssert.AreEqual(new[] { first, second }, retried.Entries!.Select(entry => entry.AudienceProfileId).ToArray());
        Assert.AreEqual(imported.SnapshotId, preview.SnapshotId);
        CollectionAssert.AreEqual(new[] { first, second }, preview.Entries!.Select(entry => entry.AudienceProfileId).ToArray());
        Assert.AreEqual(3, preview.EligibleCount, "Live candidate count keeps its original meaning.");
        // Identity projection follows current links (future previews only); the snapshot IDs do not.
        Assert.AreEqual(0, retried.Entries!.Single(entry => entry.AudienceProfileId == first).Identities.Count);
    }

    [TestMethod]
    public async Task DrawHistory_PreservesInteractionSource_IdempotencyIncludesIt_AndLegacyRecordsReadNull()
    {
        var source = new CoreLotteryDrawInteractionSource { RoundId = Guid.NewGuid(), SnapshotId = Guid.NewGuid(), AudienceProfileId = Guid.NewGuid() };
        var drawId = Guid.NewGuid();
        var legacyDrawId = Guid.NewGuid();
        var history = _factory.Services.GetRequiredService<CoreLotteryDrawHistoryRepository>();

        var saved = await history.SaveAsync(drawId, "ticket-1", Rules(), Participants(source));
        var retried = await history.SaveAsync(drawId, "ticket-1", Rules(), Participants(source));
        var changedSource = new CoreLotteryDrawInteractionSource { RoundId = source.RoundId, SnapshotId = source.SnapshotId, AudienceProfileId = Guid.NewGuid() };
        await Assert.ThrowsExactlyAsync<CoreLotteryDrawIdConflictException>(() => history.SaveAsync(drawId, "ticket-1", Rules(), Participants(changedSource)));
        await Assert.ThrowsExactlyAsync<CoreLotteryDrawIdConflictException>(() => history.SaveAsync(drawId, "ticket-1", Rules(), Participants(null)));
        await history.SaveAsync(legacyDrawId, "ticket-1", Rules(), Participants(null));

        Assert.IsTrue(saved.IsNew);
        Assert.IsFalse(retried.IsNew);
        // Mutating the caller's object after save must not rewrite stored evidence.
        source.AudienceProfileId = Guid.NewGuid();
        var stored = (await history.FindAsync(drawId))!.Participants.Single(participant => participant.ParticipantId == "ticket-1");
        Assert.AreNotEqual(source.AudienceProfileId, stored.InteractionSource!.AudienceProfileId);
        Assert.IsNull((await history.FindAsync(legacyDrawId))!.Participants[0].InteractionSource);

        using var client = await AdminClientAsync();
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/lottery-draw-records/{drawId:D}/participants?page=1&pageSize=50");
        var interactionSource = page.GetProperty("items")[0].GetProperty("interactionSource");
        Assert.AreEqual(stored.InteractionSource.AudienceProfileId, interactionSource.GetProperty("audienceProfileId").GetGuid());
        var legacy = await client.GetFromJsonAsync<JsonElement>($"/api/lottery-draw-records/{legacyDrawId:D}/participants?page=1&pageSize=50");
        Assert.IsTrue(!legacy.GetProperty("items")[0].TryGetProperty("interactionSource", out var legacySource) || legacySource.ValueKind == JsonValueKind.Null);
        using var anonymous = _factory.CreateClient();
        var denied = await anonymous.GetAsync($"/api/lottery-draw-records/{drawId:D}/participants");
        CollectionAssert.Contains(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }, denied.StatusCode);
    }

    [TestMethod]
    public async Task DrawRecordEndpoint_AcceptsSourceRoundTripsIt_AndRejectsEmptySourceIds()
    {
        using var client = await AdminClientAsync();
        var roundId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var drawId = Guid.NewGuid();

        var created = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(drawId, new { roundId, snapshotId, audienceProfileId = profileId }));
        var retried = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(drawId, new { roundId, snapshotId, audienceProfileId = profileId }));
        var conflicting = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(drawId, new { roundId, snapshotId, audienceProfileId = Guid.NewGuid() }));
        var invalid = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(Guid.NewGuid(), new { roundId, snapshotId = Guid.Empty, audienceProfileId = profileId }));

        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, retried.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, conflicting.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        var stored = (await _factory.Services.GetRequiredService<CoreLotteryDrawHistoryRepository>().FindAsync(drawId))!;
        var source = stored.Participants.Single(participant => participant.ParticipantId == "ticket-1").InteractionSource!;
        Assert.AreEqual(roundId, source.RoundId);
        Assert.AreEqual(snapshotId, source.SnapshotId);
        Assert.AreEqual(profileId, source.AudienceProfileId);
        Assert.IsNull(stored.Participants.Single(participant => participant.ParticipantId == "ticket-2").InteractionSource);
    }

    [TestMethod]
    public async Task DrawRecordEndpoint_PersistsPoolSnapshotVersion_IdempotencyIncludesIt_AndLegacyReadsNull()
    {
        using var client = await AdminClientAsync();
        var source = new { roundId = Guid.NewGuid(), snapshotId = Guid.NewGuid(), audienceProfileId = Guid.NewGuid() };
        var lockedAtUtc = new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
        var version = new { lockHash = "0123456789abcdef", lockedAtUtc = "2026-11-10T08:00:00.000Z" };
        var drawId = Guid.NewGuid();

        var created = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(drawId, source, version));
        var retried = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(drawId, source, version));
        var changedHash = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(drawId, source, version with { lockHash = "fedcba9876543210" }));
        var changedTime = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(drawId, source, version with { lockedAtUtc = "2026-11-10T08:00:01.000Z" }));
        var missing = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(drawId, source));
        var invalid = await client.PostAsJsonAsync("/api/lottery-draw-records", DrawRequest(Guid.NewGuid(), source, version with { lockHash = "not-a-hash" }));

        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, retried.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, changedHash.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, changedTime.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);

        var createdVersion = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("poolSnapshotVersion");
        Assert.AreEqual(version.lockHash, createdVersion.GetProperty("lockHash").GetString());
        var summary = await client.GetFromJsonAsync<JsonElement>($"/api/lottery-draw-records/{drawId:D}/summary");
        var summaryVersion = summary.GetProperty("poolSnapshotVersion");
        Assert.AreEqual(version.lockHash, summaryVersion.GetProperty("lockHash").GetString());
        Assert.AreEqual(lockedAtUtc, summaryVersion.GetProperty("lockedAtUtc").GetDateTimeOffset());

        var history = _factory.Services.GetRequiredService<CoreLotteryDrawHistoryRepository>();
        var stored = (await history.FindAsync(drawId))!;
        Assert.AreEqual(version.lockHash, stored.PoolSnapshotVersion!.LockHash);
        Assert.AreEqual(lockedAtUtc, stored.PoolSnapshotVersion.LockedAtUtc);

        // Unlocked/legacy draws keep a null version; adding a version on retry is a conflict, not an upgrade.
        var legacyDrawId = Guid.NewGuid();
        await history.SaveAsync(legacyDrawId, "ticket-1", Rules(), Participants(null));
        Assert.IsNull((await history.FindAsync(legacyDrawId))!.PoolSnapshotVersion);
        await Assert.ThrowsExactlyAsync<CoreLotteryDrawIdConflictException>(() => history.SaveAsync(legacyDrawId, "ticket-1", Rules(), Participants(null),
            new CoreLotteryDrawPoolSnapshotVersion { LockHash = version.lockHash, LockedAtUtc = lockedAtUtc }));
        var legacy = await client.GetFromJsonAsync<JsonElement>($"/api/lottery-draw-records/{legacyDrawId:D}/summary");
        Assert.IsTrue(!legacy.TryGetProperty("poolSnapshotVersion", out var legacyVersion) || legacyVersion.ValueKind == JsonValueKind.Null);
        Assert.AreEqual("Imported viewer", legacy.GetProperty("winnerDisplayName").GetString());

        // The paginated admin list carries the same version (null for the legacy draw).
        var list = await client.GetFromJsonAsync<JsonElement>("/api/lottery-draw-records?page=1&pageSize=100");
        var items = list.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("drawId").GetGuid());
        var listVersion = items[drawId].GetProperty("poolSnapshotVersion");
        Assert.AreEqual(version.lockHash, listVersion.GetProperty("lockHash").GetString());
        Assert.AreEqual(lockedAtUtc, listVersion.GetProperty("lockedAtUtc").GetDateTimeOffset());
        Assert.IsTrue(!items[legacyDrawId].TryGetProperty("poolSnapshotVersion", out var legacyListVersion) || legacyListVersion.ValueKind == JsonValueKind.Null);
    }

    private EligibilitySnapshotService Snapshots => _factory.Services.GetRequiredService<EligibilitySnapshotService>();
    private InteractionRoundService Rounds => _factory.Services.GetRequiredService<InteractionRoundService>();
    private IdentityLinkService Links => _factory.Services.GetRequiredService<IdentityLinkService>();
    private IInteractionsYamlDocumentRepository Interactions => _factory.Services.GetRequiredService<IInteractionsYamlDocumentRepository>();

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await client.GetFromJsonAsync<JsonElement>("/api/test/session-token");
        client.DefaultRequestHeaders.Add("X-EasyLottery-Session-Token", token.GetProperty("token").GetString());
        return client;
    }

    private async Task<Guid> CreateRoundAsync(string name) =>
        (await Rounds.CreateAsync(new CreateInteractionRoundRequest($"{name} round", "SignIn", "!join", null, null), CancellationToken.None)).Id;

    /// <summary>Creates a profile plus platform identity; the draft round rejects the event but #303 still records the identity.</summary>
    private async Task<Guid> CreateProfileAsync(Guid roundId, string platform, string channelScope, string externalUserId)
    {
        var profileId = Guid.NewGuid();
        await Rounds.ProcessAudienceEventAsync(roundId, new AudienceInteractionRequest(profileId, platform, channelScope, externalUserId, $"event-{Guid.NewGuid():N}", "!join"), CancellationToken.None);
        return profileId;
    }

    private static CoreLotteryDrawRuleSnapshot Rules() => new()
    {
        AlgorithmVersion = "secure-random-index-v1",
        EligibilityPolicyVersion = "not-previously-won-v1",
        GroupWinnerPolicyVersion = "exclude-lottery-group-after-win-v1",
        EligibleEntryCount = 2,
        PrizeSelectionPolicyVersion = "first-remaining-prize-v1",
        PrizeQueue = ["Prize"]
    };

    private static List<CoreLotteryDrawParticipantSnapshot> Participants(CoreLotteryDrawInteractionSource? source) =>
    [
        new() { ParticipantId = "ticket-1", LotteryGroupId = "profile-group", DisplayName = "Imported viewer", InteractionSource = source, EligibilityStatus = "eligible" },
        new() { ParticipantId = "ticket-2", LotteryGroupId = "manual-group", DisplayName = "Manual viewer", EligibilityStatus = "eligible" }
    ];

    private static object DrawRequest(Guid drawId, object source, object? poolSnapshotVersion = null) => new
    {
        drawId,
        poolSnapshotVersion,
        winnerParticipantId = "ticket-1",
        ruleSnapshot = new
        {
            algorithmVersion = "secure-random-index-v1",
            eligibilityPolicyVersion = "not-previously-won-v1",
            groupWinnerPolicyVersion = "exclude-lottery-group-after-win-v1",
            eligibleEntryCount = 2,
            prizeSelectionPolicyVersion = "first-remaining-prize-v1",
            prizeQueue = new[] { "Prize" }
        },
        participants = new object[]
        {
            new { participantId = "ticket-1", lotteryGroupId = "profile-group", displayName = "Imported viewer", level = "", group = "", tags = Array.Empty<string>(), eligibilityStatus = "eligible", interactionSource = source },
            new { participantId = "ticket-2", lotteryGroupId = "manual-group", displayName = "Manual viewer", level = "", group = "", tags = Array.Empty<string>(), eligibilityStatus = "eligible" }
        }
    };
}
