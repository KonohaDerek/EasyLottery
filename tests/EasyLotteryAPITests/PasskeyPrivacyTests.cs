using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using EasyLotteryApi.Passkey;
using EasyLotteryApi.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class PasskeyPrivacyTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task UntrustedLoginOptions_DoNotDistinguishAdminEmail(int credentialCount)
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        for (var index = 0; index < credentialCount; index++)
            await store.AddCredentialAsync(Credential(index));
        using var client = factory.CreateClient();
        var admin = await Options(client, "admin@example.com");
        var other = await Options(client, "other@example.com");

        Assert.AreEqual(NormalizedShape(admin), NormalizedShape(other));
        Assert.AreEqual("login", admin.GetProperty("flow").GetString());
        Assert.IsFalse(admin.GetRawText().Contains("registration_required"));
        Assert.IsFalse(other.GetRawText().Contains("admin_email_required"));
        var adminIds = CredentialIds(admin);
        var otherIds = CredentialIds(other);
        CollectionAssert.AreEqual(adminIds.Select(id => Convert.FromBase64String(ToBase64(id)).Length).ToArray(),
            otherIds.Select(id => Convert.FromBase64String(ToBase64(id)).Length).ToArray());
        Assert.IsFalse(adminIds.Intersect(otherIds).Any());
    }

    [TestMethod]
    public async Task DecoyCredentialIds_AreStableAcrossRequestsAndRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-privacy-{Guid.NewGuid():N}");
        string[] first;
        await using (var factory = CreateFactory(directory))
        {
            using var client = factory.CreateClient();
            first = CredentialIds(await Options(client, "other@example.com"));
            CollectionAssert.AreEqual(first, CredentialIds(await Options(client, " OTHER@example.com ")));
        }
        await using var restarted = CreateFactory(directory);
        using var nextClient = restarted.CreateClient();
        CollectionAssert.AreEqual(first, CredentialIds(await Options(nextClient, "other@example.com")));
        Assert.IsFalse(first.Intersect(CredentialIds(await Options(nextClient, "different@example.com"))).Any());
    }

    [TestMethod]
    public async Task DecoyOptionsAndVerify_PreserveRealPendingChallenge()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.AddCredentialAsync(Credential(0));
        using var client = factory.CreateClient();
        await Options(client, "admin@example.com");
        var original = (await store.ReadAsync()).Authentication!;
        await Options(client, "other@example.com");
        using var failed = await Verify(client, "other@example.com");
        Assert.AreEqual(HttpStatusCode.Unauthorized, failed.StatusCode);
        var pending = await store.TakePendingAsync(PasskeyFlow.Login);
        Assert.IsNotNull(pending);
        Assert.AreEqual(original.OptionsJson, pending.OptionsJson);
        Assert.AreEqual("admin@example.com", pending.Email);
    }

    [TestMethod]
    public async Task LoginVerify_UsesUniformFailureForUnknownEmailChallengeAndCredential()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<PasskeyStateStore>();
        await store.AddCredentialAsync(Credential(0));
        using var client = factory.CreateClient();
        using var unknownEmail = await Verify(client, "other@example.com");
        using var noChallenge = await Verify(client, "admin@example.com");
        await Options(client, "admin@example.com");
        using var invalidCredential = await Verify(client, "admin@example.com");
        var expected = await unknownEmail.Content.ReadAsStringAsync();
        foreach (var response in new[] { unknownEmail, noChallenge, invalidCredential })
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(expected, await response.Content.ReadAsStringAsync());
        }
        StringAssert.Contains(expected, "passkey_verification_failed");
        Assert.IsNull(await store.TakePendingAsync(PasskeyFlow.Login));
    }

    [TestMethod]
    [DataRow("options")]
    [DataRow("verify")]
    public async Task UntrustedRegistration_IsRejectedBeforeEmailValidation(string endpoint)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string? first = null;
        foreach (var email in new[] { "admin@example.com", "other@example.com", "" })
        {
            using var response = await client.PostAsJsonAsync($"/api/auth/passkey/{endpoint}",
                new { email, flow = "register", credential = new { } });
            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "registration_not_allowed");
            if (first is not null) Assert.AreEqual(first, body);
            first = body;
        }
    }

    [TestMethod]
    public async Task MalformedLoginEmail_IsRejectedWithoutAdminDisclosure()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        foreach (var email in new[] { "", "not-an-email", "Owner <admin@example.com>" })
        {
            using var response = await client.PostAsJsonAsync("/api/auth/passkey/options", new { email, flow = "login" });
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), "invalid_email");
        }
    }

    private static async Task<JsonElement> Options(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/passkey/options", new { email, flow = "login" });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static Task<HttpResponseMessage> Verify(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/passkey/verify", new { email, flow = "login", credential = new { } });

    private static string[] CredentialIds(JsonElement body) =>
        body.GetProperty("options").GetProperty("allowCredentials").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!).ToArray();

    private static string ToBase64(string value)
    {
        var converted = value.Replace('-', '+').Replace('_', '/');
        return converted.PadRight((converted.Length + 3) / 4 * 4, '=');
    }

    private static string NormalizedShape(JsonElement body)
    {
        var node = JsonNode.Parse(body.GetRawText())!;
        node["options"]!["challenge"] = "random-challenge";
        foreach (var credential in node["options"]!["allowCredentials"]!.AsArray())
            credential!["id"] = "opaque-id";
        return node.ToJsonString();
    }

    private static PasskeyCredentialRecord Credential(int index) => new()
    {
        CredentialId = Enumerable.Repeat((byte)(index + 1), 32 + index * 16).ToArray(),
        PublicKey = [4, 5, 6],
        UserHandle = [7, 8, 9]
    };

    private static WebApplicationFactory<Program> CreateFactory(string? directory = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            var values = new Dictionary<string, string?>
            {
                ["Storage:Directory"] = directory ?? Path.Combine(Path.GetTempPath(), $"easy-lottery-privacy-{Guid.NewGuid():N}"),
                ["Security:AdminToken:Mode"] = AdminTokenSecurityOptions.PublicMode,
                ["Testing:EnableAdminSessionToken"] = "true",
                ["Testing:AuthenticationPermitLimit"] = "600"
            };
            foreach (var pair in values) builder.UseSetting(pair.Key, pair.Value);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(values));
        });
}
