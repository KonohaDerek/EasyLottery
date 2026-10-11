using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi.Security;
using EasyLotteryApi.Security.Endpoints;
using EasyLotteryApplication.Interactions;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Interactions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests.Interactions;

[TestClass]
public sealed class IdentityLinkEndpointTests
{
    [TestMethod]
    public async Task Complete_ConsumesJsonBodyCodeOnceWithoutPuttingItInUrl()
    {
        var storageDirectory = Path.Combine(Path.GetTempPath(), $"easy-lottery-identity-link-tests-{Guid.NewGuid():N}");
        try
        {
            await using var factory = CreateFactory(storageDirectory);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, await LoginAsync(factory));

            var sourceProfile = new AudienceProfile { DisplayName = "Source viewer" };
            var targetProfile = new AudienceProfile { DisplayName = "Target viewer" };
            var source = new PlatformIdentity(PlatformKind.YouTube, "youtube-user", "channel", sourceProfile.Id, sourceProfile.DisplayName);
            var target = new PlatformIdentity(PlatformKind.Twitch, "twitch-user", "channel", targetProfile.Id, targetProfile.DisplayName);
            using (var scope = factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IInteractionsYamlDocumentRepository>();
                var document = await repository.ReadAsync();
                document.AudienceProfiles.AddRange([sourceProfile, targetProfile]);
                document.PlatformIdentities.AddRange([source, target]);
                await repository.SaveAsync(document);
            }

            using var issue = await client.PostAsJsonAsync("/api/interactions/identity-links", new { sourceIdentityId = source.Id, targetIdentityId = target.Id });
            issue.EnsureSuccessStatusCode();
            var link = await issue.Content.ReadFromJsonAsync<LinkCodeResult>();

            using var complete = await client.PostAsJsonAsync("/api/interactions/identity-links/complete", new { code = link!.Code });
            Assert.AreEqual(HttpStatusCode.NoContent, complete.StatusCode);
            Assert.AreEqual("/api/interactions/identity-links/complete", complete.RequestMessage?.RequestUri?.AbsolutePath);

            using var replay = await client.PostAsJsonAsync("/api/interactions/identity-links/complete", new { code = link.Code });
            Assert.AreEqual(HttpStatusCode.BadRequest, replay.StatusCode);

            using var verificationScope = factory.Services.CreateScope();
            var verified = await verificationScope.ServiceProvider.GetRequiredService<IInteractionsYamlDocumentRepository>().ReadAsync();
            Assert.AreEqual(targetProfile.Id, verified.PlatformIdentities.Single(identity => identity.Id == source.Id).AudienceProfileId);
        }
        finally
        {
            if (Directory.Exists(storageDirectory)) Directory.Delete(storageDirectory, recursive: true);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string storageDirectory) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:EnableAdminSessionToken"] = "true",
                ["AdminAuth:SigningKey"] = "test-signing-key-for-identity-link-1234567890",
                ["Storage:Directory"] = storageDirectory
            })));

    private static async Task<string> LoginAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/test/session-token");
        var session = await response.Content.ReadFromJsonAsync<SessionTokenResponse>();
        return session!.Token;
    }
}
