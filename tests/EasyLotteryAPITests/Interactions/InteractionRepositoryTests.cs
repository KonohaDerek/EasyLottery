using EasyLotteryDomain.Models.Interactions;
using EasyLotteryApplication.Interactions;
using EasyLotteryInfrastructure.Interactions;
using EasyLotteryInfrastructure.Storage;
using EasyLotteryInfrastructure.Settings;
using EasyLotteryInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryApiTests.Interactions;

[TestClass]
public sealed class InteractionRepositoryTests
{
    [TestMethod]
    public async Task ApplyAsync_PersistsIdentityRoundAndProcessedEventAcrossReload()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"easy-lottery-interactions-{Guid.NewGuid():N}.db");
        try
        {
            var options = Options.Create(new StorageProviderOptions
            {
                Provider = "sqlite",
                ConnectionString = $"Data Source={databasePath}"
            });
            await new SqliteSchemaMigrator(options, NullLogger<SqliteSchemaMigrator>.Instance)
                .StartAsync(CancellationToken.None);

            var profile = new AudienceProfile { Id = Guid.NewGuid(), DisplayName = "Derek" };
            var identity = new PlatformIdentity(PlatformKind.YouTube, "UC123", "channel-abc", profile.Id, "Derek");
            var round = new InteractionRound { Id = Guid.NewGuid(), Name = "Vote" };
            var interactionEvent = new InteractionEvent(
                PlatformKind.YouTube,
                "channel-abc",
                "event-1",
                identity.ExternalUserId,
                "!vote blue");
            var decision = InteractionDecision.Allow(profile.Id, round.Id, "vote", InteractionRoundStatus.Live, InteractionRoundState.Empty);

            var repository = new SqliteInteractionRepository(options);
            await repository.ApplyAsync(interactionEvent, decision, profile, identity, round);

            var reloaded = await new SqliteInteractionRepository(options).ReadAsync();

            Assert.AreEqual("Derek", reloaded.AudienceProfiles.Single().DisplayName);
            Assert.AreEqual(profile.Id, reloaded.PlatformIdentities.Single().AudienceProfileId);
            Assert.AreEqual(round.Id, reloaded.Rounds.Single().Id);
            CollectionAssert.Contains(reloaded.ProcessedEventKeys, "YouTube:channel-abc:event-1");
        }
        finally
        {
            foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [TestMethod]
    public async Task ApplyAsync_RoundTripsYamlRepositoryAcrossReload()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-interactions-yaml-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Directory"] = directory })
                .Build();
            var documents = new YamlInteractionsDocumentRepository(configuration, new TestEnvironment(), new StorageGateProvider());
            var profile = new AudienceProfile { Id = Guid.NewGuid(), DisplayName = "Ari" };
            var identity = new PlatformIdentity(PlatformKind.Twitch, "twitch-1", "channel-abc", profile.Id, "Ari");
            var round = new InteractionRound { Id = Guid.NewGuid(), Name = "Sign in" };
            var interactionEvent = new InteractionEvent(PlatformKind.Twitch, "channel-abc", "event-2", identity.ExternalUserId, "!join");
            var decision = InteractionDecision.Allow(profile.Id, round.Id, "join", InteractionRoundStatus.Live, InteractionRoundState.Empty);

            await new YamlInteractionRepository(documents).ApplyAsync(interactionEvent, decision, profile, identity, round);

            var reloaded = await new YamlInteractionRepository(
                new YamlInteractionsDocumentRepository(configuration, new TestEnvironment(), new StorageGateProvider()))
                .ReadAsync();

            Assert.AreEqual("Ari", reloaded.AudienceProfiles.Single().DisplayName);
            CollectionAssert.Contains(reloaded.ProcessedEventKeys, "Twitch:channel-abc:event-2");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void DefaultStorageProvider_SelectsYamlInteractionRepository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-interactions-di-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Directory"] = directory })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IHostEnvironment>(new TestEnvironment());
            services.AddEasyLotteryInfrastructure();
            using var provider = services.BuildServiceProvider();

            Assert.IsInstanceOfType<YamlInteractionRepository>(provider.GetRequiredService<IInteractionRepository>());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "EasyLotteryApiTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
