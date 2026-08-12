using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EasyLotteryApi.Passkey;
using EasyLotteryApi.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SecurityEndpointTests
{
    [TestMethod]
    public async Task LegacySessionTokenEndpoint_DoesNotIssueAdminToken()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/session-token");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task PasskeyRegistration_PublicModeWithoutAllowlist_IsDenied()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Security:AdminToken:Mode"] = AdminTokenSecurityOptions.PublicMode
        });
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "admin@example.com", flow = "register" });

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PasskeyRegistration_PublicMode_AllowsAllowlistedClient()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Security:AdminToken:Mode"] = AdminTokenSecurityOptions.PublicMode,
            ["Security:AdminToken:AllowedClientIps:0"] = "loopback"
        });
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "admin@example.com", flow = "register" });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PublicMode_DoesNotTrustUnconfiguredForwardedForHeader()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Security:AdminToken:Mode"] = AdminTokenSecurityOptions.PublicMode,
            ["Security:AdminToken:AllowedClientIps:0"] = "203.0.113.10"
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/passkey/options")
        {
            Content = JsonContent.Create(new { email = "admin@example.com", flow = "register" })
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PasskeyLogin_WithoutRegisteredCredential_RequiresRegistration()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "admin@example.com", flow = "login" });

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("registration_required", error!.Code);
    }

    [TestMethod]
    public async Task PasskeyEndpoints_RejectNonAdminEmail()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "other@example.com", flow = "login" });

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PasskeyOptions_RejectsInvalidFlow()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "admin@example.com", flow = "reset" });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("invalid_flow", error!.Code);
    }

    [TestMethod]
    public async Task PasskeyVerify_WithoutChallenge_IsRejected()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/passkey/verify",
            new { email = "admin@example.com", flow = "register", credential = new { } });

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("challenge_invalid", error!.Code);
    }

    [TestMethod]
    public async Task PasskeyVerify_ConsumesChallengeBeforeInvalidCredentialCanBeReplayed()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var options = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "admin@example.com", flow = "register" });
        options.EnsureSuccessStatusCode();

        var invalidCredential = new { email = "admin@example.com", flow = "register", credential = new { } };
        using var firstAttempt = await client.PostAsJsonAsync("/api/auth/passkey/verify", invalidCredential);
        using var replayAttempt = await client.PostAsJsonAsync("/api/auth/passkey/verify", invalidCredential);

        Assert.AreNotEqual(HttpStatusCode.OK, firstAttempt.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, replayAttempt.StatusCode);
        var error = await replayAttempt.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("challenge_invalid", error!.Code);
    }

    [TestMethod]
    public async Task PasskeyRegistration_RejectsSecondCredentialAfterFirstRegistration()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.SaveCredentialAsync(new PasskeyCredentialRecord
        {
            CredentialId = [1, 2, 3],
            PublicKey = [4, 5, 6],
            UserHandle = [7, 8, 9],
            SignatureCounter = 1
        });
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "admin@example.com", flow = "register" });

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("passkey_already_registered", error!.Code);
    }

    [TestMethod]
    public async Task AdminPasskeyManagement_ListsAndProtectsLastCredential()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.SaveCredentialAsync(CreateCredential([1, 2, 3], "Main Mac"));
        using var client = factory.CreateClient();

        using var withoutHeader = await client.GetAsync("/api/admin/passkeys");
        Assert.AreEqual(HttpStatusCode.Unauthorized, withoutHeader.StatusCode);

        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/admin/passkeys");
        list.Headers.Add(ObsSessionTokenService.HeaderName, LoginAsync(factory));
        using var listed = await client.SendAsync(list);
        Assert.AreEqual(HttpStatusCode.OK, listed.StatusCode);
        var passkeys = await listed.Content.ReadFromJsonAsync<List<PasskeyDeviceResponse>>();
        Assert.AreEqual(1, passkeys!.Count);
        Assert.AreEqual("Main Mac", passkeys[0].Name);

        using var remove = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/admin/passkeys/{WebEncoders.Base64UrlEncode([1, 2, 3])}");
        remove.Headers.Add(ObsSessionTokenService.HeaderName, LoginAsync(factory));
        using var removed = await client.SendAsync(remove);
        Assert.AreEqual(HttpStatusCode.Conflict, removed.StatusCode);
        var error = await removed.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("last_passkey", error!.Code);
    }

    [TestMethod]
    public async Task AdminPasskeyManagement_BeginAddRequiresAdminSession()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.SaveCredentialAsync(CreateCredential([1, 2, 3], "Main Mac"));
        using var client = factory.CreateClient();

        using var withoutHeader = await client.PostAsJsonAsync(
            "/api/admin/passkeys/options",
            new { name = "Backup iPhone" });
        Assert.AreEqual(HttpStatusCode.Unauthorized, withoutHeader.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/passkeys/options")
        {
            Content = JsonContent.Create(new { name = "Backup iPhone" })
        };
        request.Headers.Add(ObsSessionTokenService.HeaderName, LoginAsync(factory));
        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<PasskeyBeginResult>();
        Assert.AreEqual(PasskeyFlow.Add.ToValue(), options!.Flow);
    }

    [TestMethod]
    public async Task PasskeyStateStore_SupportsMultipleCredentialsWithoutRemovingLast()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.SaveCredentialAsync(CreateCredential([1, 2, 3], "Main Mac"));
        await store.SaveCredentialAsync(CreateCredential([4, 5, 6], "Backup iPhone"));

        var state = await store.ReadAsync();
        Assert.AreEqual(2, state.Credentials.Count);
        Assert.AreEqual(PasskeyRemoveResult.Removed, await store.RemoveCredentialAsync([1, 2, 3]));
        Assert.AreEqual(PasskeyRemoveResult.LastCredential, await store.RemoveCredentialAsync([4, 5, 6]));
    }

    [TestMethod]
    public async Task PasskeyStateStore_MigratesLegacySingleCredential()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-legacy-passkey-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "admin-passkey.json"),
            JsonSerializer.Serialize(new { Credential = CreateCredential([1, 2, 3], "Legacy Mac") }));

        await using var factory = CreateFactory(new Dictionary<string, string?> { ["Storage:Directory"] = directory });
        var state = await factory.Services.GetRequiredService<PasskeyStateStore>().ReadAsync();

        Assert.AreEqual(1, state.Credentials.Count);
        Assert.AreEqual("Legacy Mac", state.Credentials[0].Name);
    }

    [TestMethod]
    public async Task AdminEndpoints_RequireHeaderIndependentAdminSession()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = LoginAsync(factory);

        using var withoutHeader = await client.GetAsync("/api/settings/visual-style");
        using var queryToken = await client.GetAsync($"/api/settings/visual-style?sessionToken={Uri.EscapeDataString(adminToken)}");

        Assert.AreEqual(HttpStatusCode.Unauthorized, withoutHeader.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, queryToken.StatusCode);
    }

    [TestMethod]
    public async Task SessionEndpoint_RevokesAdminJwt()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = LoginAsync(factory);
        using var revoke = new HttpRequestMessage(HttpMethod.Delete, "/api/session");
        revoke.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);

        using var revoked = await client.SendAsync(revoke);
        using var afterRevoke = new HttpRequestMessage(HttpMethod.Get, "/api/settings/visual-style");
        afterRevoke.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var rejected = await client.SendAsync(afterRevoke);

        Assert.AreEqual(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [TestMethod]
    public async Task TestSessionTokenEndpoint_IsDisabledUnlessExplicitlyEnabled()
    {
        await using var disabledFactory = CreateFactory();
        using var disabledClient = disabledFactory.CreateClient();
        using var disabled = await disabledClient.GetAsync("/api/test/session-token");

        await using var enabledFactory = CreateFactory(new Dictionary<string, string?>
        {
            ["Testing:EnableAdminSessionToken"] = "true"
        });
        using var enabledClient = enabledFactory.CreateClient();
        using var enabled = await enabledClient.GetAsync("/api/test/session-token");

        Assert.AreEqual(HttpStatusCode.NotFound, disabled.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, enabled.StatusCode);
        var token = await enabled.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.IsFalse(string.IsNullOrWhiteSpace(token!.Token));
        Assert.IsTrue(token.ExpiresAtUtc > DateTimeOffset.UtcNow);

        await using var productionFactory = CreateFactory(new Dictionary<string, string?>
        {
            ["Testing:EnableAdminSessionToken"] = "true"
        }, "Production");
        using var productionClient = productionFactory.CreateClient();
        using var production = await productionClient.GetAsync("/api/test/session-token");

        Assert.AreEqual(HttpStatusCode.NotFound, production.StatusCode);
    }

    [TestMethod]
    public async Task PasskeyStateStore_TakesPendingChallengeOnlyOnce()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.SetPendingAsync(PasskeyFlow.Register, new PasskeyPendingCeremony
        {
            Email = AdminPasskeyOptions.DefaultEmail,
            OptionsJson = "{}",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        var first = await store.TakePendingAsync(PasskeyFlow.Register);
        var second = await store.TakePendingAsync(PasskeyFlow.Register);

        Assert.IsNotNull(first);
        Assert.IsNull(second);
    }

    [TestMethod]
    public async Task PasskeyStateStore_DoesNotReturnExpiredChallenge()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.SetPendingAsync(PasskeyFlow.Login, new PasskeyPendingCeremony
        {
            Email = AdminPasskeyOptions.DefaultEmail,
            OptionsJson = "{}",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)
        });

        var pending = await store.TakePendingAsync(PasskeyFlow.Login);

        Assert.IsNull(pending);
    }

    [TestMethod]
    public async Task PasskeyStateStore_UpdatesMatchingCounterMonotonically()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.SaveCredentialAsync(new PasskeyCredentialRecord
        {
            CredentialId = [1, 2, 3],
            PublicKey = [4, 5, 6],
            UserHandle = [7, 8, 9],
            SignatureCounter = 10
        });

        var wrongCredential = await store.UpdateCounterAsync([9, 9, 9], 100);
        var increased = await store.UpdateCounterAsync([1, 2, 3], 20);
        var decreased = await store.UpdateCounterAsync([1, 2, 3], 5);
        var state = await store.ReadAsync();

        Assert.IsFalse(wrongCredential);
        Assert.IsTrue(increased);
        Assert.IsTrue(decreased);
        Assert.AreEqual((uint)20, state.Credential!.SignatureCounter);
    }

    [TestMethod]
    public async Task PasskeyOptions_UsesConfiguredAdminEmail()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["admin.email"] = "owner@example.com"
        });
        using var client = factory.CreateClient();

        using var allowed = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "OWNER@example.com", flow = "register" });
        using var denied = await client.PostAsJsonAsync(
            "/api/auth/passkey/options",
            new { email = "admin@example.com", flow = "register" });

        Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [TestMethod]
    public async Task ObsToken_CannotWriteSettingsOrManageTunnel()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = LoginAsync(factory);
        using var issue = new HttpRequestMessage(HttpMethod.Post, "/api/obs-sessions")
        {
            Content = JsonContent.Create(new { resourceKind = "donate", resourceId = Guid.NewGuid().ToString(), scopes = new[] { "read", "control" } })
        };
        issue.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var issued = await client.SendAsync(issue);
        var obsToken = (await issued.Content.ReadFromJsonAsync<TokenResponse>())!.Token;

        using var settings = new HttpRequestMessage(HttpMethod.Put, "/settings") { Content = new StringContent("{}") };
        settings.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var settingsResult = await client.SendAsync(settings);
        using var tunnel = new HttpRequestMessage(HttpMethod.Post, "/api/tunnel/start") { Content = JsonContent.Create(new { provider = "none" }) };
        tunnel.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var tunnelResult = await client.SendAsync(tunnel);

        Assert.AreEqual(HttpStatusCode.Forbidden, settingsResult.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, tunnelResult.StatusCode);
    }

    [TestMethod]
    public async Task ObsToken_ReadsOnlyItsBoundResourceWithoutSecrets()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = LoginAsync(factory);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var document = new EasyLotteryConfigDocument
        {
            DonateLotteryActivities =
            [
                new DonateLotteryActivity { Id = 1, PublicId = firstId, Name = "Scoped Activity" },
                new DonateLotteryActivity { Id = 2, PublicId = secondId, Name = "Other Activity" }
            ]
        };
        document.SystemSettings.MailDelivery.SmtpPassword = "must-not-leak";
        var yaml = YamlSerialization.CreateSerializerBuilder().Build().Serialize(document);
        using var save = new HttpRequestMessage(HttpMethod.Put, "/settings") { Content = new StringContent(yaml) };
        save.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var saved = await client.SendAsync(save);
        saved.EnsureSuccessStatusCode();
        var obsToken = await IssueObsTokenAsync(client, adminToken, "donate", firstId.ToString(), ["read"]);

        using var read = new HttpRequestMessage(HttpMethod.Get, "/settings");
        read.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var response = await client.SendAsync(read);
        var projectedYaml = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(projectedYaml, "Scoped Activity");
        Assert.IsFalse(projectedYaml.Contains("Other Activity", StringComparison.Ordinal));
        Assert.IsFalse(projectedYaml.Contains("must-not-leak", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PasskeyOptionsRateLimit_ReturnsTooManyRequests()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            response?.Dispose();
            response = await client.PostAsJsonAsync(
                "/api/auth/passkey/options",
                new { email = "admin@example.com", flow = "login" });
        }

        using (response)
            Assert.AreEqual(HttpStatusCode.TooManyRequests, response!.StatusCode);
    }

    [TestMethod]
    public async Task OversizedRequest_IsRejected()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new ByteArrayContent(new byte[1_048_577]);
        using var response = await client.PutAsync("/settings", content);

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IReadOnlyDictionary<string, string?>? settings = null,
        string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            var values = new Dictionary<string, string?>
            {
                ["Storage:Directory"] = Path.Combine(Path.GetTempPath(), $"easy-lottery-security-tests-{Guid.NewGuid():N}")
            };
            if (settings is not null)
            {
                foreach (var setting in settings) values[setting.Key] = setting.Value;
            }

            foreach (var setting in values) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        });

    private static string LoginAsync(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;

    private static PasskeyCredentialRecord CreateCredential(byte[] id, string name) => new()
    {
        CredentialId = id,
        PublicKey = [4, 5, 6],
        UserHandle = [7, 8, 9],
        SignatureCounter = 1,
        Name = name,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static async Task<string> IssueObsTokenAsync(HttpClient client, string adminToken, string kind, string resourceId, string[] scopes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/obs-sessions")
        {
            Content = JsonContent.Create(new { resourceKind = kind, resourceId, scopes })
        };
        request.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
    }

    private sealed record TokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
    private sealed record ErrorResponse(string? Error, string? Code);
}
