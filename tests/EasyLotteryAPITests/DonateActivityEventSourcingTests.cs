using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure;
using EasyLotteryInfrastructure.DonateActivities;
using EasyLotteryInfrastructure.Storage;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class DonateActivityEventSourcingTests
{
    [TestMethod]
    public async Task SaveCommand_AppendsEvent_AndUpdatesSnapshot()
    {
        var services = CreateServices();
        var mediator = services.GetRequiredService<IMediator>();
        var repository = services.GetRequiredService<IDonateLotteryActivityRepository>();
        var eventStore = services.GetRequiredService<IDonateActivityEventStore>();

        var saved = await mediator.Send(new SaveDonateActivityCommand(new DonateLotteryActivity
        {
            Name = "一番賞",
            MinimumDonationAmount = 100m,
            StartsAtUtc = new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero),
            EndsAtUtc = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero),
            Prizes =
            [
                new DonateLotteryPrize { Name = "A賞", Quantity = 1, RemainingQuantity = 1, Probability = 50m },
                new DonateLotteryPrize { Name = "B賞", Quantity = 2, RemainingQuantity = 2, Probability = 30m }
            ]
        }));

        Assert.IsTrue(saved.Id > 0);

        var activities = await repository.ListAsync();
        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual(saved.Id, activities[0].Id);

        var events = await eventStore.ReadAllAsync();
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(DonateActivityEventTypes.Saved, events[0].Type);
        Assert.AreEqual(saved.Id, events[0].ActivityId);
        Assert.IsTrue(File.Exists(((YamlDonateActivityEventStore)eventStore).EventLogPath));
        Assert.IsTrue(File.Exists(((YamlDonateActivityRepository)repository).SnapshotPath));
    }

    [TestMethod]
    public async Task DeleteCommand_AppendsDeletedEvent_AndRemovesProjection()
    {
        var services = CreateServices();
        var mediator = services.GetRequiredService<IMediator>();
        var repository = services.GetRequiredService<IDonateLotteryActivityRepository>();
        var eventStore = services.GetRequiredService<IDonateActivityEventStore>();

        var saved = await mediator.Send(new SaveDonateActivityCommand(new DonateLotteryActivity
        {
            Name = "一番賞",
            MinimumDonationAmount = 100m,
            StartsAtUtc = new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero),
            EndsAtUtc = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero)
        }));

        await mediator.Send(new DeleteDonateActivityCommand(saved.Id));

        var activities = await repository.ListAsync();
        Assert.AreEqual(0, activities.Count);

        var events = await eventStore.ReadAllAsync();
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(DonateActivityEventTypes.Deleted, events[1].Type);
    }

    private static ServiceProvider CreateServices()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-cqrs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Directory"] = directory })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestEnvironment(directory));
        services.AddEasyLotteryInfrastructure();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
            typeof(GetDonateActivitiesQuery).Assembly,
            typeof(DependencyInjection).Assembly));
        return services.BuildServiceProvider();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public TestEnvironment(string contentRootPath) => ContentRootPath = contentRootPath;

        public string ApplicationName { get; set; } = "EasyLotteryApiTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
