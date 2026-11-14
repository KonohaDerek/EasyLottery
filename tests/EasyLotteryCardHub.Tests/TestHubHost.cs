using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Endpoints;
using EasyLotteryCardHub.Hosting;
using EasyLotteryCardHub.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EasyLotteryCardHub.Tests;

internal sealed class TestHubHost : IAsyncDisposable
{
    private static readonly string SigningSecret = "test-card-hub-signing-key-0123456789abcdef";
    private static readonly string AdminKey = "test-card-hub-platform-admin-key-0123456789";
    private readonly string _schema;
    private readonly IHost _host;

    private TestHubHost(IHost host, HttpClient client, string schema, string connectionString, MutableTimeProvider clock)
    {
        _host = host;
        Client = client;
        _schema = schema;
        ConnectionString = connectionString;
        Clock = clock;
    }

    public static PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("card_hub_tests")
        .WithUsername("postgres")
        .WithPassword("cardhub_test_password")
        .Build();

    public static string PlatformAdminKey => AdminKey;
    public HttpClient Client { get; }
    public string ConnectionString { get; }
    public MutableTimeProvider Clock { get; }
    public IServiceProvider Services => _host.Services;
    public CapturingViewerEmailSender Emails => Services.GetRequiredService<CapturingViewerEmailSender>();

    public static async Task StartContainerAsync() => await Container.StartAsync();

    public static async Task StopContainerAsync() => await Container.DisposeAsync();

    public static async Task<TestHubHost> CreateAsync(MutableTimeProvider? clock = null)
    {
        clock ??= new MutableTimeProvider(DateTimeOffset.UtcNow);
        var schema = "hubtest_" + Guid.NewGuid().ToString("N");
        await using (var adminConnection = new NpgsqlConnection(Container.GetConnectionString()))
        {
            await adminConnection.OpenAsync();
            await using var createSchema = adminConnection.CreateCommand();
            createSchema.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await createSchema.ExecuteNonQueryAsync();
        }

        var connection = new NpgsqlConnectionStringBuilder(Container.GetConnectionString()) { SearchPath = schema };
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:CardHub"] = connection.ConnectionString,
            ["Authentication:SigningKey"] = SigningSecret,
            ["Authentication:Issuer"] = "https://card-hub.test",
            ["PlatformAdmin:BootstrapEmail"] = "admin@example.test",
            ["PlatformAdmin:BootstrapToken"] = AdminKey,
            ["Passkey:RpId"] = "localhost",
            ["Passkey:ServerName"] = "Card Hub Test",
            ["Passkey:Origins:0"] = "http://localhost:18940",
            ["Database:RunMigrationsOnStartup"] = "true"
        };

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .UseEnvironment("Testing")
                .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(settings))
                .ConfigureServices((context, services) =>
                {
                    services.AddCardHubServices(context.Configuration, connection.ConnectionString, clock);
                    services.AddSingleton<CapturingViewerEmailSender>();
                    services.AddSingleton<IViewerEmailSender>(provider => provider.GetRequiredService<CapturingViewerEmailSender>());
                })
                .Configure(app =>
                {
                    app.UseCardHubRequestPipeline();
                    app.UseEndpoints(endpoints => endpoints.MapHubEndpoints());
                }))
            .StartAsync();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await db.Database.MigrateAsync();
            await HubHostingExtensions.SeedBootstrapAdminAsync(db, host.Services.GetRequiredService<IConfiguration>(), clock);
        }

        var client = host.GetTestClient();
        var ready = await client.GetAsync("/health/ready");
        if (!ready.IsSuccessStatusCode)
        {
            var body = await ready.Content.ReadAsStringAsync();
            client.Dispose();
            await host.StopAsync();
            host.Dispose();
            await DropSchemaAsync(schema);
            throw new InvalidOperationException($"Card Hub test host failed readiness ({(int)ready.StatusCode}): {body}");
        }

        return new TestHubHost(host, client, schema, connection.ConnectionString, clock);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
        await DropSchemaAsync(_schema);
    }

    private static async Task DropSchemaAsync(string schema)
    {
        await using var connection = new NpgsqlConnection(Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed record CapturedViewerEmail(string Recipient, ViewerLoginCodeEmail Email);

internal sealed class CapturingViewerEmailSender : IViewerEmailSender
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<CapturedViewerEmail> _messages = new();
    public IReadOnlyList<CapturedViewerEmail> Messages => _messages.ToArray();

    public Task SendLoginCodeAsync(string recipient, ViewerLoginCodeEmail email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _messages.Enqueue(new CapturedViewerEmail(recipient, email));
        return Task.CompletedTask;
    }
}

internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan duration) => _now = _now.Add(duration);
}
