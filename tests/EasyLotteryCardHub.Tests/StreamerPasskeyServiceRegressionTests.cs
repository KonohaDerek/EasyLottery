using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EasyLotteryCardHub.Endpoints;
using EasyLotteryCardHub.Hosting;
using EasyLotteryCardHub.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace EasyLotteryCardHub.Tests;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class StreamerPasskeyServiceRegressionTests
{
    private const string PlatformAdminKey = "test-card-hub-platform-admin-key-0123456789";
    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("card_hub_passkey_tests")
        .WithUsername("postgres")
        .WithPassword("cardhub_test_password")
        .Build();

    [ClassInitialize]
    public static async Task StartPostgreSql(TestContext _) => await Container.StartAsync();

    [ClassCleanup]
    public static async Task StopPostgreSql() => await Container.DisposeAsync();

    [TestMethod]
    public async Task RegistrationOptionsRequireUserVerification()
    {
        await using var host = await HubTestHost.CreateAsync();
        using var inviteRequest = new HttpRequestMessage(HttpMethod.Post, "/api/platform-admin/invites")
        {
            Content = JsonContent.Create(new { email = "streamer@example.test", expiresInHours = 24 })
        };
        inviteRequest.Headers.Add("X-Platform-Admin-Key", PlatformAdminKey);
        using var inviteResponse = await host.Client.SendAsync(inviteRequest);
        Assert.AreEqual(HttpStatusCode.Created, inviteResponse.StatusCode, await inviteResponse.Content.ReadAsStringAsync());
        using var inviteDocument = JsonDocument.Parse(await inviteResponse.Content.ReadAsStringAsync());
        var inviteToken = inviteDocument.RootElement.GetProperty("inviteToken").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(inviteToken));

        using var optionsResponse = await host.Client.PostAsJsonAsync("/api/auth/streamer-registration/passkey/options", new
        {
            inviteToken,
            displayName = "Streamer",
            slug = "streamer-one"
        });
        Assert.AreEqual(HttpStatusCode.OK, optionsResponse.StatusCode, await optionsResponse.Content.ReadAsStringAsync());
        using var optionsDocument = JsonDocument.Parse(await optionsResponse.Content.ReadAsStringAsync());
        var authenticatorSelection = optionsDocument.RootElement.GetProperty("options").GetProperty("authenticatorSelection");
        Assert.AreEqual("required", authenticatorSelection.GetProperty("userVerification").GetString());
    }

    private sealed class HubTestHost(IHost host) : IAsyncDisposable
    {
        public HttpClient Client { get; } = host.GetTestClient();

        public static async Task<HubTestHost> CreateAsync()
        {
            var connectionString = Container.GetConnectionString();
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:CardHub"] = connectionString,
                ["Authentication:SigningKey"] = "test-card-hub-signing-key-0123456789abcdef",
                ["Authentication:Issuer"] = "https://card-hub.test",
                ["PlatformAdmin:BootstrapEmail"] = "admin@example.test",
                ["PlatformAdmin:BootstrapToken"] = PlatformAdminKey,
                ["Passkey:RpId"] = "localhost",
                ["Passkey:ServerName"] = "Card Hub Test",
                ["Passkey:Origins:0"] = "http://localhost:18940"
            };

            var host = await new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .UseEnvironment("Testing")
                    .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(settings))
                    .ConfigureServices((context, services) =>
                        services.AddCardHubServices(context.Configuration, connectionString))
                    .Configure(app =>
                    {
                        app.UseCardHubRequestPipeline();
                        app.UseEndpoints(endpoints => endpoints.MapHubEndpoints());
                    }))
                .StartAsync();

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EasyLotteryCardHub.Data.HubDbContext>();
                await db.Database.MigrateAsync();
                await HubHostingExtensions.SeedBootstrapAdminAsync(
                    db,
                    host.Services.GetRequiredService<IConfiguration>(),
                    TimeProvider.System);
            }

            return new HubTestHost(host);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await host.StopAsync();
            host.Dispose();
        }
    }
}
