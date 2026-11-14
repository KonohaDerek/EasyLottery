using System.Net;
using System.Net.Http.Json;
using EasyLotteryCardHub.Email;
using EasyLotteryCardHub.Invites;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryCardHubTests;

[TestClass]
public sealed class AdminAuthorizationTests
{
    [TestMethod]
    public async Task AdminRoutesRequireExplicitApiKeyAndDoNotExposeDirectTenantCreation()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CardHub:Admin:ApiKey"] = "test-admin-key-not-used-by-request",
                ["CardHub:EmailOutbox:EncryptionKey"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
                ["CardHub:Tokens:SigningKey"] = Convert.ToBase64String(Enumerable.Range(32, 32).Select(value => (byte)value).ToArray())
            }));
        });
        using var client = factory.CreateClient();

        var denied = await client.PostAsJsonAsync("/hub/v1/admin/streamer-invites", new CreateStreamerInviteRequest("owner@example.test", "owner", "Owner", 72));
        var directTenantCreation = await client.PostAsJsonAsync("/hub/v1/admin/streamers", new { slug = "owner", displayName = "Owner" });

        Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, directTenantCreation.StatusCode);
    }
}

internal sealed class FakeInviteEmailSender : IInviteEmailSender
{
    public List<InviteEmailPayload> Messages { get; } = [];

    public Task SendAsync(InviteEmailPayload message, CancellationToken cancellationToken)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}
