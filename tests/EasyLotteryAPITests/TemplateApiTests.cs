using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi.Security;
using EasyLotteryDomain.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class TemplateApiTests
{
    [TestMethod]
    public async Task PokeTemplateRestApi_UsesResourceCrudAndRejectsMismatchedIds()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var token = LoginAsync(factory);
        AddToken(client, token);

        using var created = await client.PostAsJsonAsync("/api/poke-templates", new PokeTemplate { Name = "REST 戳戳樂" });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var template = (await created.Content.ReadFromJsonAsync<PokeTemplate>())!;
        Assert.AreNotEqual(Guid.Empty, template.PublicId);

        using var mismatch = await client.PutAsJsonAsync($"/api/poke-templates/{template.Id}", new PokeTemplate { Id = template.Id + 1, Name = "錯誤" });
        Assert.AreEqual(HttpStatusCode.BadRequest, mismatch.StatusCode);

        using var read = await client.GetAsync($"/api/poke-templates/{template.Id}");
        Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
        using var deleted = await client.DeleteAsync($"/api/poke-templates/{template.Id}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [TestMethod]
    public async Task ObsTemplateEndpoint_IsBoundToPublicIdAndScope()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = LoginAsync(factory);
        AddToken(client, adminToken);
        using var created = await client.PostAsJsonAsync("/api/roulette-templates", new RouletteTemplate { Name = "REST 轉盤" });
        var template = (await created.Content.ReadFromJsonAsync<RouletteTemplate>())!;

        var obsToken = await IssueObsTokenAsync(client, adminToken, "roulette", template.PublicId.ToString(), ["read"]);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/roulette-templates/public/{template.PublicId}");
        request.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var spinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/roulette-templates/public/{template.PublicId}/spin")
        {
            Content = JsonContent.Create(new { forceIndex = (int?)null, currentRotation = 0d })
        };
        spinRequest.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var denied = await client.SendAsync(spinRequest);
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [TestMethod]
    public async Task ActivityResults_IsAvailableAsReadOnlyResource()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, LoginAsync(factory));
        using var response = await client.GetAsync("/api/activity-results");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(await response.Content.ReadFromJsonAsync<List<object>>());
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Directory"] = Path.Combine(Path.GetTempPath(), $"easy-lottery-template-tests-{Guid.NewGuid():N}")
            })));

    private static string LoginAsync(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;

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

    private static void AddToken(HttpClient client, string token) => client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);

    private sealed record TokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
}
