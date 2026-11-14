using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Endpoints;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EasyLotteryCardHub.Tests;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class HubFoundationIntegrationTests
{
    [ClassInitialize]
    public static async Task StartPostgreSql(TestContext _) => await TestHubHost.StartContainerAsync();

    [ClassCleanup]
    public static async Task StopPostgreSql() => await TestHubHost.StopContainerAsync();

    [TestMethod]
    public async Task InviteIsEmailBoundSingleUseAndExpires()
    {
        await using var host = await TestHubHost.CreateAsync();
        using var inviteRequest = new HttpRequestMessage(HttpMethod.Post, "/api/platform-admin/invites")
        {
            Content = JsonContent.Create(new { email = "Streamer@Example.test", expiresInHours = 24 })
        };
        inviteRequest.Headers.Add("X-Platform-Admin-Key", TestHubHost.PlatformAdminKey);
        using var inviteResponse = await host.Client.SendAsync(inviteRequest);
        Assert.AreEqual(HttpStatusCode.Created, inviteResponse.StatusCode);
        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<CreatedInviteResponse>();
        Assert.IsNotNull(inviteBody);
        Assert.AreEqual("streamer@example.test", inviteBody.Email);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var invite = await db.StreamerInvites.AsNoTracking().SingleAsync(item => item.Id == inviteBody.Id);
        Assert.AreEqual(SecretHasher.Hash(inviteBody.InviteToken), invite.TokenHash);
        Assert.IsFalse(invite.TokenHash.Contains(inviteBody.InviteToken, StringComparison.Ordinal));

        using var passkeyOptionsRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/streamer-registration/passkey/options")
        {
            Content = JsonContent.Create(new { inviteToken = inviteBody.InviteToken, displayName = "Streamer", slug = "streamer-one" })
        };
        using var passkeyOptionsResponse = await host.Client.SendAsync(passkeyOptionsRequest);
        Assert.AreEqual(HttpStatusCode.OK, passkeyOptionsResponse.StatusCode);
        using var passkeyOptionsDocument = JsonDocument.Parse(await passkeyOptionsResponse.Content.ReadAsStringAsync());
        var passkeyRoot = passkeyOptionsDocument.RootElement;
        Assert.AreNotEqual(Guid.Empty, passkeyRoot.GetProperty("flowId").GetGuid());
        Assert.AreEqual("streamer@example.test", passkeyRoot.GetProperty("options").GetProperty("user").GetProperty("name").GetString());
        Assert.IsTrue(passkeyRoot.GetProperty("options").TryGetProperty("challenge", out _));

        var redemption = scope.ServiceProvider.GetRequiredService<InviteRedemptionService>();
        var tenant = await redemption.RedeemAsync(
            invite.TokenHash,
            "Streamer",
            "streamer-one",
            [1, 2, 3],
            [4, 5, 6],
            [7, 8, 9],
            0,
            CancellationToken.None);
        Assert.AreEqual("streamer@example.test", tenant.EmailNormalized);
        Assert.AreEqual(TenantStatus.Active, tenant.Status);
        await Assert.ThrowsExactlyAsync<HubServiceException>(() => redemption.RedeemAsync(
            invite.TokenHash,
            "Duplicate",
            "streamer-two",
            [10],
            [11],
            [12],
            0,
            CancellationToken.None));

        var adminId = await db.PlatformAdmins.Select(item => item.Id).SingleAsync();
        var expiredTokenHash = SecretHasher.Hash(SecretHasher.CreateInviteToken());
        db.StreamerInvites.Add(new StreamerInvite
        {
            Id = Guid.NewGuid(),
            Email = "expired@example.test",
            EmailNormalized = "expired@example.test",
            TokenHash = expiredTokenHash,
            CreatedByPlatformAdminId = adminId,
            CreatedAt = host.Clock.GetUtcNow().AddHours(-2),
            ExpiresAt = host.Clock.GetUtcNow().AddHours(-1)
        });
        await db.SaveChangesAsync();
        var expired = await Assert.ThrowsExactlyAsync<HubServiceException>(() => redemption.RedeemAsync(
            expiredTokenHash,
            "Expired",
            "expired-streamer",
            [13],
            [14],
            [15],
            0,
            CancellationToken.None));
        Assert.AreEqual("invite_invalid_or_expired", expired.Code);
        Assert.IsFalse(await db.Streamers.AnyAsync(item => item.EmailNormalized == "expired@example.test"));

        using var raceInviteRequest = new HttpRequestMessage(HttpMethod.Post, "/api/platform-admin/invites")
        {
            Content = JsonContent.Create(new { email = "race@example.test", expiresInHours = 24 })
        };
        raceInviteRequest.Headers.Add("X-Platform-Admin-Key", TestHubHost.PlatformAdminKey);
        using var raceInviteResponse = await host.Client.SendAsync(raceInviteRequest);
        Assert.AreEqual(HttpStatusCode.Created, raceInviteResponse.StatusCode);
        var raceInvite = await raceInviteResponse.Content.ReadFromJsonAsync<CreatedInviteResponse>();
        Assert.IsNotNull(raceInvite);
        var raceHash = SecretHasher.Hash(raceInvite.InviteToken);

        async Task<bool> TryRedeemInviteAsync(string tenantSlug)
        {
            await using var raceScope = host.Services.CreateAsyncScope();
            var raceRedemption = raceScope.ServiceProvider.GetRequiredService<InviteRedemptionService>();
            try
            {
                await raceRedemption.RedeemAsync(raceHash, "Race Streamer", tenantSlug, [16], [17], [18], 0, CancellationToken.None);
                return true;
            }
            catch (HubServiceException)
            {
                return false;
            }
        }

        var raceResults = await Task.WhenAll(TryRedeemInviteAsync("race-one"), TryRedeemInviteAsync("race-two"));
        Assert.AreEqual(1, raceResults.Count(result => result));
    }

    [TestMethod]
    public async Task SiteCredentialRotationAllowsTwentyFourHourOverlapAndRevocationIsImmediate()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await TestHubHost.CreateAsync(clock);
        var tenantId = await CreateTenantAsync(host, "rotation@example.test", "rotation");
        RegisteredSite firstSite;
        RegisteredSiteCredential firstRotation;
        using (var scope = host.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<SiteCredentialService>();
            firstSite = await service.RegisterSiteAsync(tenantId, "Rotation site", "https://rotation.example.test", false, CancellationToken.None);
            firstRotation = await service.RotateAsync(tenantId, firstSite.Id, CancellationToken.None);
            Assert.AreEqual(firstSite.ClientId, firstRotation.ClientId);
            Assert.AreEqual(SecretHasher.Hash(firstSite.ClientSecret), await ReadCredentialHashAsync(host, firstSite.CredentialId));
            Assert.AreEqual(SecretHasher.Hash(firstRotation.ClientSecret), await ReadCredentialHashAsync(host, firstRotation.CredentialId));

            var oldToken = await service.ExchangeClientCredentialsAsync(firstSite.ClientId, firstSite.ClientSecret, CancellationToken.None);
            var newToken = await service.ExchangeClientCredentialsAsync(firstSite.ClientId, firstRotation.ClientSecret, CancellationToken.None);
            Assert.IsFalse(string.IsNullOrWhiteSpace(oldToken.AccessToken));
            Assert.IsFalse(string.IsNullOrWhiteSpace(newToken.AccessToken));
            await Assert.ThrowsExactlyAsync<HubServiceException>(() => service.RotateAsync(tenantId, firstSite.Id, CancellationToken.None));

            var secondSite = await service.RegisterSiteAsync(tenantId, "Revocation site", "https://revocation.example.test", false, CancellationToken.None);
            var secondRotation = await service.RotateAsync(tenantId, secondSite.Id, CancellationToken.None);
            var previousToken = await ExchangeTokenAsync(host, secondSite.ClientId, secondSite.ClientSecret);
            var currentToken = await ExchangeTokenAsync(host, secondSite.ClientId, secondRotation.ClientSecret);

            Assert.IsTrue(await service.RevokeAsync(tenantId, secondSite.Id, secondSite.CredentialId, CancellationToken.None));
            var revokedPrevious = await Assert.ThrowsExactlyAsync<HubServiceException>(() => service.ExchangeClientCredentialsAsync(secondSite.ClientId, secondSite.ClientSecret, CancellationToken.None));
            Assert.AreEqual("invalid_client", revokedPrevious.Code);
            var previousCall = await RequestSitePingAsync(host, previousToken);
            Assert.AreEqual(HttpStatusCode.Unauthorized, previousCall.StatusCode);
            var previousError = await previousCall.Content.ReadFromJsonAsync<HubApiError>();
            Assert.AreEqual("site_credential_revoked", previousError?.Code);
            previousCall.Dispose();

            using var currentCall = await RequestSitePingAsync(host, currentToken);
            Assert.AreEqual(HttpStatusCode.OK, currentCall.StatusCode);
            Assert.IsTrue(await service.RevokeAsync(tenantId, secondSite.Id, secondRotation.CredentialId, CancellationToken.None));
            using var revokedCurrentCall = await RequestSitePingAsync(host, currentToken);
            Assert.AreEqual(HttpStatusCode.Unauthorized, revokedCurrentCall.StatusCode);
            var revokedCurrent = await Assert.ThrowsExactlyAsync<HubServiceException>(() => service.ExchangeClientCredentialsAsync(secondSite.ClientId, secondRotation.ClientSecret, CancellationToken.None));
            Assert.AreEqual("invalid_client", revokedCurrent.Code);
        }

        clock.Advance(TimeSpan.FromHours(24).Add(TimeSpan.FromSeconds(1)));
        using (var scope = host.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<SiteCredentialService>();
            var expired = await Assert.ThrowsExactlyAsync<HubServiceException>(() => service.ExchangeClientCredentialsAsync(firstSite.ClientId, firstSite.ClientSecret, CancellationToken.None));
            Assert.AreEqual("invalid_client", expired.Code);
            Assert.IsNotNull(await service.ExchangeClientCredentialsAsync(firstSite.ClientId, firstRotation.ClientSecret, CancellationToken.None));
        }
    }

    [TestMethod]
    public async Task DisabledTenantIsRejectedOnEverySiteRequestWithTypedForbiddenError()
    {
        await using var host = await TestHubHost.CreateAsync();
        var tenantId = await CreateTenantAsync(host, "disabled@example.test", "disabled");
        var site = await RegisterSiteAsync(host, tenantId, "Disabled site", "https://disabled.example.test");
        var siteToken = await ExchangeTokenAsync(host, site.ClientId, site.ClientSecret);

        using var disableRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform-admin/tenants/{tenantId:D}/status")
        {
            Content = JsonContent.Create(new { status = "disabled" })
        };
        disableRequest.Headers.Add("X-Platform-Admin-Key", TestHubHost.PlatformAdminKey);
        using var disableResponse = await host.Client.SendAsync(disableRequest);
        Assert.AreEqual(HttpStatusCode.OK, disableResponse.StatusCode);

        using var siteRequest = new HttpRequestMessage(HttpMethod.Get, "/api/site/ping");
        siteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", siteToken);
        using var siteResponse = await host.Client.SendAsync(siteRequest);
        Assert.AreEqual(HttpStatusCode.Forbidden, siteResponse.StatusCode);
        var error = await siteResponse.Content.ReadFromJsonAsync<HubApiError>();
        Assert.IsNotNull(error);
        Assert.AreEqual("tenant_disabled", error.Code);
    }

    [TestMethod]
    public async Task StreamerAndSiteTokensCannotCrossTheirAudienceBoundaries()
    {
        await using var host = await TestHubHost.CreateAsync();
        var tenantId = await CreateTenantAsync(host, "isolation@example.test", "isolation");
        var site = await RegisterSiteAsync(host, tenantId, "Isolation site", "https://isolation.example.test");
        var siteToken = await ExchangeTokenAsync(host, site.ClientId, site.ClientSecret);
        string streamerToken;
        using (var scope = host.Services.CreateScope())
        {
            streamerToken = scope.ServiceProvider.GetRequiredService<HubTokenService>().IssueStreamerToken(tenantId).AccessToken;
        }

        using var siteWithStreamerToken = new HttpRequestMessage(HttpMethod.Get, "/api/site/ping");
        siteWithStreamerToken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", streamerToken);
        using var siteResult = await host.Client.SendAsync(siteWithStreamerToken);
        Assert.AreEqual(HttpStatusCode.Forbidden, siteResult.StatusCode);

        using var streamerWithSiteToken = new HttpRequestMessage(HttpMethod.Get, "/api/streamer/me");
        streamerWithSiteToken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", siteToken);
        using var streamerResult = await host.Client.SendAsync(streamerWithSiteToken);
        Assert.AreEqual(HttpStatusCode.Forbidden, streamerResult.StatusCode);
    }

    [TestMethod]
    public async Task OAuthTokenEndpointUsesStandardClientCredentialsResponseAndErrorFields()
    {
        await using var host = await TestHubHost.CreateAsync();
        var tenantId = await CreateTenantAsync(host, "oauth@example.test", "oauth");
        var site = await RegisterSiteAsync(host, tenantId, "OAuth site", "https://oauth.example.test");
        _ = await ExchangeTokenAsync(host, site.ClientId, site.ClientSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{site.ClientId}:incorrect-secret")));
        using var response = await host.Client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.IsTrue(response.Headers.WwwAuthenticate.Any(item => item.Scheme == "Basic"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("invalid_client", document.RootElement.GetProperty("error").GetString());
        Assert.IsTrue(document.RootElement.TryGetProperty("error_description", out _));
    }

    [TestMethod]
    public async Task AuditEventsRejectUpdatesAndDeletes()
    {
        await using var host = await TestHubHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var admin = await db.PlatformAdmins.SingleAsync();
        var audit = new AuditEvent
        {
            EventType = "test.append_only",
            ActorType = "test",
            ActorId = "integration-test",
            CreatedAt = host.Clock.GetUtcNow(),
            DetailsJson = "{}"
        };
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync();
        audit.DetailsJson = "{\"mutated\":true}";
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(audit).State = EntityState.Unchanged;

        await using var connection = new NpgsqlConnection(host.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE audit_events SET \"DetailsJson\" = '{}'::jsonb WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", audit.Id);
        await Assert.ThrowsExactlyAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        command.CommandText = "TRUNCATE audit_events";
        command.Parameters.Clear();
        await Assert.ThrowsExactlyAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.IsNotNull(admin);
    }

    [TestMethod]
    public async Task HealthEndpointsReportLivenessAndDatabaseReadiness()
    {
        await using var host = await TestHubHost.CreateAsync();
        using var live = await host.Client.GetAsync("/health/live");
        using var ready = await host.Client.GetAsync("/health/ready");
        Assert.AreEqual(HttpStatusCode.OK, live.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, ready.StatusCode);
    }

    [TestMethod]
    public async Task ViewerCodeRequestDoesNotRevealAccountExistenceAndUsesDeckStreamerCopy()
    {
        await using var host = await TestHubHost.CreateAsync();
        var tenantId = await CreateTenantAsync(host, "copy-streamer@example.test", "copy-streamer");
        _ = await CreateViewerAsync(host, "existing-viewer@example.test");

        using var newRequest = await RequestViewerCodeAsync(host, "new-viewer@example.test", tenantId);
        using var existingRequest = await RequestViewerCodeAsync(host, "existing-viewer@example.test");
        Assert.AreEqual(HttpStatusCode.Accepted, newRequest.StatusCode);
        Assert.AreEqual(HttpStatusCode.Accepted, existingRequest.StatusCode);
        Assert.AreEqual(await newRequest.Content.ReadAsStringAsync(), await existingRequest.Content.ReadAsStringAsync());

        var message = host.Emails.Messages.Single(item => item.Recipient == "new-viewer@example.test").Email;
        Assert.AreEqual("viewer.loginCode.email.subject.streamer", message.SubjectKey);
        StringAssert.Contains(message.Subject, "copy-streamer");
        StringAssert.Contains(message.Body, "你正在從 copy-streamer 的卡牌活動登入 Card Hub。");
        CollectionAssert.Contains(message.BodyKeys.ToArray(), "viewer.loginCode.email.body.intro.streamer");
        StringAssert.Contains(message.Subject, ExtractLoginCode(message.Body));

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<HubTokenSecurityOptions>();
        var emailHash = ViewerSensitiveHasher.HashEmail("new-viewer@example.test", options.SigningKey.Key);
        var code = await db.ViewerLoginCodes.SingleAsync(item => item.EmailHash == emailHash);
        Assert.AreNotEqual("new-viewer@example.test", code.EmailHash);
        Assert.AreNotEqual(ExtractLoginCode(message.Body), code.CodeHash);
    }

    [TestMethod]
    public async Task ViewerCodeResendCooldownIsEnforcedForSixtySeconds()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await TestHubHost.CreateAsync(clock);
        using var first = await RequestViewerCodeAsync(host, "cooldown@example.test");
        Assert.AreEqual(HttpStatusCode.Accepted, first.StatusCode);

        using var early = await RequestViewerCodeAsync(host, "cooldown@example.test");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, early.StatusCode);
        var earlyError = await early.Content.ReadFromJsonAsync<ViewerApiError>();
        Assert.AreEqual("viewer.code.resend.tooSoon", earlyError?.MessageKey);

        clock.Advance(TimeSpan.FromSeconds(60));
        using var afterCooldown = await RequestViewerCodeAsync(host, "cooldown@example.test");
        Assert.AreEqual(HttpStatusCode.Accepted, afterCooldown.StatusCode);
        Assert.AreEqual(2, host.Emails.Messages.Count);
    }

    [TestMethod]
    public async Task ViewerCodeExpiresAfterTenMinutes()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await TestHubHost.CreateAsync(clock);
        using var request = await RequestViewerCodeAsync(host, "expired-viewer@example.test");
        var code = ExtractLoginCode(host.Emails.Messages.Single().Email.Body);
        clock.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));

        using var verify = await host.Client.PostAsJsonAsync("/api/auth/viewer/code/verify", new { email = "expired-viewer@example.test", loginCode = code });
        Assert.AreEqual(HttpStatusCode.BadRequest, verify.StatusCode);
        var error = await verify.Content.ReadFromJsonAsync<ViewerApiError>();
        Assert.AreEqual("viewer.code.error.expired", error?.MessageKey);
    }

    [TestMethod]
    public async Task FiveWrongViewerCodesInvalidateTheCodeAndThrottleFurtherAttempts()
    {
        await using var host = await TestHubHost.CreateAsync();
        const string email = "attempts@example.test";
        using var request = await RequestViewerCodeAsync(host, email);
        var expectedCode = ExtractLoginCode(host.Emails.Messages.Single().Email.Body);
        var wrongCode = expectedCode == "000000" ? "000001" : "000000";

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var wrong = await host.Client.PostAsJsonAsync("/api/auth/viewer/code/verify", new { email, loginCode = wrongCode });
            Assert.AreEqual(HttpStatusCode.BadRequest, wrong.StatusCode);
            var error = await wrong.Content.ReadFromJsonAsync<ViewerApiError>();
            Assert.AreEqual("viewer.code.error.wrongWithRemaining", error?.MessageKey);
        }

        using var fifth = await host.Client.PostAsJsonAsync("/api/auth/viewer/code/verify", new { email, loginCode = wrongCode });
        Assert.AreEqual(HttpStatusCode.BadRequest, fifth.StatusCode);
        Assert.AreEqual("viewer.code.error.tooManyAttempts", (await fifth.Content.ReadFromJsonAsync<ViewerApiError>())?.MessageKey);

        using var sixth = await host.Client.PostAsJsonAsync("/api/auth/viewer/code/verify", new { email, loginCode = expectedCode });
        Assert.AreEqual(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.AreEqual("viewer.code.error.rateLimited", (await sixth.Content.ReadFromJsonAsync<ViewerApiError>())?.MessageKey);
    }

    [TestMethod]
    public async Task ViewerRegistrationRequiresAgeAndTermsAndRejectsUnderThirteen()
    {
        await using var host = await TestHubHost.CreateAsync();
        const string email = "young-viewer@example.test";
        using var request = await RequestViewerCodeAsync(host, email);
        var code = ExtractLoginCode(host.Emails.Messages.Single().Email.Body);

        using var noAge = await host.Client.PostAsJsonAsync("/api/auth/viewer/code/verify", new { email, loginCode = code });
        Assert.AreEqual(HttpStatusCode.BadRequest, noAge.StatusCode);
        Assert.AreEqual("viewer.signup.age.required", (await noAge.Content.ReadFromJsonAsync<ViewerApiError>())?.MessageKey);

        using var noTerms = await host.Client.PostAsJsonAsync("/api/auth/viewer/code/verify", new { email, loginCode = code, ageBracket = "18_plus" });
        Assert.AreEqual(HttpStatusCode.BadRequest, noTerms.StatusCode);
        Assert.AreEqual("viewer.signup.terms.required", (await noTerms.Content.ReadFromJsonAsync<ViewerApiError>())?.MessageKey);

        using var underThirteen = await host.Client.PostAsJsonAsync("/api/auth/viewer/code/verify", new { email, loginCode = code, ageBracket = "under13", termsAccepted = true, termsVersion = "1" });
        Assert.AreEqual(HttpStatusCode.Forbidden, underThirteen.StatusCode);
        Assert.AreEqual("viewer.signup.under13.body", (await underThirteen.Content.ReadFromJsonAsync<ViewerApiError>())?.MessageKey);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.IsFalse(await db.Viewers.AnyAsync(item => item.EmailNormalized == email));
    }

    [TestMethod]
    public async Task ViewerRegistrationIssuesAudienceSpecificTokensAndRefreshRotationRevokesReuse()
    {
        await using var host = await TestHubHost.CreateAsync();
        const string email = "login-viewer@example.test";
        using var request = await RequestViewerCodeAsync(host, email);
        var code = ExtractLoginCode(host.Emails.Messages.Single().Email.Body);
        using var verify = await host.Client.PostAsJsonAsync("/api/auth/viewer/code/verify", new
        {
            email,
            loginCode = code,
            ageBracket = "13_17",
            termsAccepted = true,
            termsVersion = "1"
        });
        Assert.AreEqual(HttpStatusCode.OK, verify.StatusCode);
        var session = await verify.Content.ReadFromJsonAsync<ViewerSessionResponse>();
        Assert.IsNotNull(session);
        var accessToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.AreEqual(HubTokenAudiences.Viewer, accessToken.Audiences.Single());
        Assert.AreEqual("viewer", accessToken.Claims.Single(item => item.Type == "token_kind").Value);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var viewer = await db.Viewers.SingleAsync(item => item.Id == session.ViewerId);
        Assert.AreEqual("login-viewer@example.test", viewer.EmailNormalized);
        Assert.AreEqual("13_17", viewer.AgeBracket);
        Assert.AreEqual("1", viewer.TermsVersionAccepted);
        var firstRefresh = await db.ViewerRefreshTokens.SingleAsync(item => item.ViewerId == session.ViewerId);
        Assert.AreEqual(SecretHasher.Hash(session.RefreshToken), firstRefresh.TokenHash);
        Assert.IsFalse(firstRefresh.TokenHash.Contains(session.RefreshToken, StringComparison.Ordinal));

        using var refresh = await host.Client.PostAsJsonAsync("/api/auth/viewer/refresh", new { refreshToken = session.RefreshToken });
        Assert.AreEqual(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<ViewerSessionResponse>();
        Assert.IsNotNull(rotated);
        Assert.AreNotEqual(session.RefreshToken, rotated.RefreshToken);
        Assert.AreEqual(HubTokenAudiences.Viewer, new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(rotated.AccessToken).Audiences.Single());

        using var replay = await host.Client.PostAsJsonAsync("/api/auth/viewer/refresh", new { refreshToken = session.RefreshToken });
        Assert.AreEqual(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.IsTrue(await db.ViewerRefreshTokens.Where(item => item.SessionFamilyId == firstRefresh.SessionFamilyId)
            .AllAsync(item => item.RevokedAt != null));
    }

    [TestMethod]
    public async Task ViewerStreamerSiteAndPlatformAdminTokensAreMutuallyIsolated()
    {
        await using var host = await TestHubHost.CreateAsync();
        var viewer = await CreateViewerAsync(host, "isolated-viewer@example.test");
        var tenantId = await CreateTenantAsync(host, "viewer-isolation@example.test", "viewer-isolation");
        var site = await RegisterSiteAsync(host, tenantId, "Viewer isolation", "https://viewer-isolation.example.test");
        var siteToken = await ExchangeTokenAsync(host, site.ClientId, site.ClientSecret);
        string streamerToken;
        using (var scope = host.Services.CreateScope())
            streamerToken = scope.ServiceProvider.GetRequiredService<HubTokenService>().IssueStreamerToken(tenantId).AccessToken;

        using var viewerToStreamer = ViewerRequest(HttpMethod.Get, "/api/streamer/me", viewer.AccessToken);
        using var viewerToSite = ViewerRequest(HttpMethod.Get, "/api/site/ping", viewer.AccessToken);
        using var viewerToAdmin = ViewerRequest(HttpMethod.Post, "/api/platform-admin/invites", viewer.AccessToken);
        viewerToAdmin.Content = JsonContent.Create(new { email = "no-admin@example.test" });
        using var streamerToViewer = ViewerRequest(HttpMethod.Get, "/api/viewer/groups", streamerToken);
        using var siteToViewer = ViewerRequest(HttpMethod.Get, "/api/viewer/groups", siteToken);
        using var adminToViewer = new HttpRequestMessage(HttpMethod.Get, "/api/viewer/groups");
        adminToViewer.Headers.Add("X-Platform-Admin-Key", TestHubHost.PlatformAdminKey);

        foreach (var endpointRequest in new[] { viewerToStreamer, viewerToSite, viewerToAdmin, streamerToViewer, siteToViewer, adminToViewer })
        {
            using var endpointResponse = await host.Client.SendAsync(endpointRequest);
            Assert.IsTrue(endpointResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"Expected viewer/admin token isolation, received {(int)endpointResponse.StatusCode}.");
        }
    }

    [TestMethod]
    public async Task ViewerGroupsAreScopedCrudAndExportIsNoStoreAndOwnDataOnly()
    {
        await using var host = await TestHubHost.CreateAsync();
        var owner = await CreateViewerAsync(host, "export-owner@example.test");
        var other = await CreateViewerAsync(host, "export-other@example.test");
        using (var seedScope = host.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<HubDbContext>();
            seedDb.ViewerGroups.Add(new ViewerGroup
            {
                Id = Guid.NewGuid(), ViewerId = other.Id, Name = "Other private group", NameNormalized = "OTHER PRIVATE GROUP",
                CreatedAt = host.Clock.GetUtcNow(), UpdatedAt = host.Clock.GetUtcNow()
            });
            await seedDb.SaveChangesAsync();
        }

        using var create = ViewerRequest(HttpMethod.Post, "/api/viewer/groups", owner.AccessToken);
        create.Content = JsonContent.Create(new { name = "Owned cards" });
        using var created = await host.Client.SendAsync(create);
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var group = await created.Content.ReadFromJsonAsync<ViewerGroupResponse>();
        Assert.IsNotNull(group);

        var cardId = Guid.NewGuid();
        var otherCardId = Guid.NewGuid();
        using (var itemScope = host.Services.CreateScope())
        {
            var itemDb = itemScope.ServiceProvider.GetRequiredService<HubDbContext>();
            var otherGroupId = await itemDb.ViewerGroups.Where(item => item.ViewerId == other.Id).Select(item => item.Id).SingleAsync();
            itemDb.ViewerGroupItems.AddRange(
                new ViewerGroupItem { GroupId = group.Id, CardInstanceId = cardId, AddedAt = host.Clock.GetUtcNow() },
                new ViewerGroupItem { GroupId = otherGroupId, CardInstanceId = otherCardId, AddedAt = host.Clock.GetUtcNow() });
            await itemDb.SaveChangesAsync();
        }

        using var listGroups = ViewerRequest(HttpMethod.Get, "/api/viewer/groups", owner.AccessToken);
        using var groupList = await host.Client.SendAsync(listGroups);
        Assert.AreEqual(HttpStatusCode.OK, groupList.StatusCode);
        var withItem = (await groupList.Content.ReadFromJsonAsync<ViewerGroupResponse[]>())!.Single(item => item.Id == group.Id);
        CollectionAssert.AreEqual(new[] { cardId }, withItem.CardInstanceIds.ToArray());

        using var otherUpdate = ViewerRequest(HttpMethod.Put, $"/api/viewer/groups/{group.Id:D}", other.AccessToken);
        otherUpdate.Content = JsonContent.Create(new { name = "Taken group" });
        using var otherResult = await host.Client.SendAsync(otherUpdate);
        Assert.AreEqual(HttpStatusCode.NotFound, otherResult.StatusCode);

        using var exportRequest = ViewerRequest(HttpMethod.Get, "/api/viewer/account/export", owner.AccessToken);
        using var exportResponse = await host.Client.SendAsync(exportRequest);
        Assert.AreEqual(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.IsTrue(exportResponse.Headers.CacheControl?.NoStore == true);
        var export = await exportResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(export, "export-owner@example.test");
        StringAssert.Contains(export, cardId.ToString());
        StringAssert.Contains(export, "Owned cards");
        Assert.IsFalse(export.Contains("export-other@example.test", StringComparison.Ordinal));
        Assert.IsFalse(export.Contains("Other private group", StringComparison.Ordinal));
        Assert.IsFalse(export.Contains(otherCardId.ToString(), StringComparison.Ordinal));
        Assert.IsFalse(export.Contains(owner.RefreshToken, StringComparison.Ordinal));
        Assert.IsFalse(export.Contains(other.RefreshToken, StringComparison.Ordinal));

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.IsTrue(await db.AuditEvents.AnyAsync(item => item.EventType == "viewer.data_exported" && item.ActorId == owner.Id.ToString("D")));

        using var update = ViewerRequest(HttpMethod.Put, $"/api/viewer/groups/{group.Id:D}", owner.AccessToken);
        update.Content = JsonContent.Create(new { name = "Favorites" });
        using var updated = await host.Client.SendAsync(update);
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        Assert.AreEqual("Favorites", (await updated.Content.ReadFromJsonAsync<ViewerGroupResponse>())?.Name);

        using var delete = ViewerRequest(HttpMethod.Delete, $"/api/viewer/groups/{group.Id:D}", owner.AccessToken);
        using var deleted = await host.Client.SendAsync(delete);
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.IsFalse(await db.ViewerGroups.AnyAsync(item => item.Id == group.Id));
        Assert.IsFalse(await db.ViewerGroupItems.AnyAsync(item => item.GroupId == group.Id));
    }

    [TestMethod]
    public async Task ViewerEmailAndHandleUniquenessAreCaseInsensitive()
    {
        await using var host = await TestHubHost.CreateAsync();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var now = host.Clock.GetUtcNow();
        db.Viewers.Add(new Viewer
        {
            Id = Guid.NewGuid(), Email = "case@example.test", EmailNormalized = "case@example.test",
            TermsVersionAccepted = "1", TermsAcceptedAt = now, AgeBracket = "18_plus", Handle = "ReservedName", CreatedAt = now
        });
        await db.SaveChangesAsync();

        var duplicate = new Viewer
        {
            Id = Guid.NewGuid(), Email = "CASE@example.test", EmailNormalized = "case@example.test",
            TermsVersionAccepted = "1", TermsAcceptedAt = now, AgeBracket = "18_plus", Handle = "reservedname", CreatedAt = now
        };
        db.Viewers.Add(duplicate);
        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        duplicate.Email = "another@example.test";
        duplicate.EmailNormalized = "another@example.test";
        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [TestMethod]
    public async Task ViewerAccountDeletionRemovesGroupsSessionsAndCodesAndWritesAudit()
    {
        await using var host = await TestHubHost.CreateAsync();
        var viewer = await CreateViewerAsync(host, "delete-viewer@example.test");
        var emailHash = ViewerSensitiveHasher.HashEmail(viewer.Email, host.Services.GetRequiredService<HubTokenSecurityOptions>().SigningKey.Key);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var group = new ViewerGroup
            {
                Id = Guid.NewGuid(), ViewerId = viewer.Id, Name = "Delete me", NameNormalized = "DELETE ME",
                CreatedAt = host.Clock.GetUtcNow(), UpdatedAt = host.Clock.GetUtcNow()
            };
            db.ViewerGroups.Add(group);
            db.ViewerGroupItems.Add(new ViewerGroupItem { GroupId = group.Id, CardInstanceId = Guid.NewGuid(), AddedAt = host.Clock.GetUtcNow() });
            db.ViewerLoginCodes.Add(new ViewerLoginCode
            {
                Id = Guid.NewGuid(), EmailHash = emailHash, CodeHash = new string('a', 64),
                CreatedAt = host.Clock.GetUtcNow(), ExpiresAt = host.Clock.GetUtcNow().AddMinutes(10)
            });
            await db.SaveChangesAsync();
        }

        using var request = ViewerRequest(HttpMethod.Delete, "/api/viewer/account", viewer.AccessToken);
        using var response = await host.Client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);

        using var checkScope = host.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.IsFalse(await checkDb.Viewers.AnyAsync(item => item.Id == viewer.Id));
        Assert.IsFalse(await checkDb.ViewerGroups.AnyAsync(item => item.ViewerId == viewer.Id));
        Assert.IsFalse(await checkDb.ViewerGroupItems.AnyAsync());
        Assert.IsFalse(await checkDb.ViewerRefreshTokens.AnyAsync(item => item.ViewerId == viewer.Id));
        Assert.IsFalse(await checkDb.ViewerLoginCodes.AnyAsync(item => item.EmailHash == emailHash));
        Assert.IsTrue(await checkDb.AuditEvents.AnyAsync(item => item.EventType == "viewer.account_deleted" && item.ActorId == viewer.Id.ToString("D")));

        using var oldToken = ViewerRequest(HttpMethod.Get, "/api/viewer/groups", viewer.AccessToken);
        using var rejected = await host.Client.SendAsync(oldToken);
        Assert.AreEqual(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    private static HttpRequestMessage ViewerRequest(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static Task<HttpResponseMessage> RequestViewerCodeAsync(TestHubHost host, string email, Guid? tenantId = null) =>
        host.Client.PostAsJsonAsync("/api/auth/viewer/code", new { email, tenantId });

    private static string ExtractLoginCode(string body)
    {
        var match = Regex.Match(body, "你的驗證碼是：(?<code>[0-9]{6})", RegexOptions.CultureInvariant);
        Assert.IsTrue(match.Success, "The development email should contain a six-digit login code.");
        return match.Groups["code"].Value;
    }

    private static async Task<TestViewerAccount> CreateViewerAsync(TestHubHost host, string email)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var normalized = email.Trim().ToLowerInvariant();
        var viewer = new Viewer
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            EmailNormalized = normalized,
            TermsVersionAccepted = "1",
            TermsAcceptedAt = host.Clock.GetUtcNow(),
            AgeBracket = "18_plus",
            CreatedAt = host.Clock.GetUtcNow()
        };
        var refreshToken = SecretHasher.CreateViewerRefreshToken();
        db.Viewers.Add(viewer);
        db.ViewerRefreshTokens.Add(new ViewerRefreshToken
        {
            Id = Guid.NewGuid(),
            ViewerId = viewer.Id,
            SessionFamilyId = Guid.NewGuid(),
            TokenHash = SecretHasher.Hash(refreshToken),
            CreatedAt = host.Clock.GetUtcNow(),
            ExpiresAt = host.Clock.GetUtcNow().AddDays(30)
        });
        await db.SaveChangesAsync();
        var accessToken = scope.ServiceProvider.GetRequiredService<HubTokenService>().IssueViewerToken(viewer.Id).AccessToken;
        return new TestViewerAccount(viewer.Id, normalized, accessToken, refreshToken);
    }

    private sealed record TestViewerAccount(Guid Id, string Email, string AccessToken, string RefreshToken);

    private static async Task<Guid> CreateTenantAsync(TestHubHost host, string email, string slug)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var normalized = email.ToLowerInvariant();
        var tenant = new StreamerTenant
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            EmailNormalized = normalized,
            DisplayName = slug,
            Slug = slug,
            Status = TenantStatus.Active,
            CreatedAt = host.Clock.GetUtcNow(),
            UpdatedAt = host.Clock.GetUtcNow()
        };
        db.Streamers.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static async Task<RegisteredSite> RegisterSiteAsync(TestHubHost host, Guid tenantId, string name, string url)
    {
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<SiteCredentialService>();
        return await service.RegisterSiteAsync(tenantId, name, url, false, CancellationToken.None);
    }

    private static async Task<string> ExchangeTokenAsync(TestHubHost host, string clientId, string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}")));
        using var response = await host.Client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("Bearer", document.RootElement.GetProperty("token_type").GetString());
        Assert.IsTrue(document.RootElement.GetProperty("expires_in").GetInt32() > 0);
        return document.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task<HttpResponseMessage> RequestSitePingAsync(TestHubHost host, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/site/ping");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await host.Client.SendAsync(request);
    }

    private static async Task<string> ReadCredentialHashAsync(TestHubHost host, Guid credentialId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        return await db.SiteCredentials.AsNoTracking().Where(item => item.Id == credentialId).Select(item => item.SecretHash).SingleAsync();
    }
}
