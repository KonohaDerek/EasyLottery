using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
