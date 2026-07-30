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

    [TestMethod]
    public async Task SaveCommand_PersistsUpdatedAnimationDatesAndDurations()
    {
        var services = CreateServices();
        var mediator = services.GetRequiredService<IMediator>();
        var repository = services.GetRequiredService<IDonateLotteryActivityRepository>();
        var saved = await mediator.Send(new SaveDonateActivityCommand(new DonateLotteryActivity
        {
            Name = "活動", MinimumDonationAmount = 100m, StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1), EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1)
        }));
        var expectedStart = new DateTimeOffset(2026, 7, 26, 3, 0, 0, TimeSpan.Zero);
        var expectedEnd = new DateTimeOffset(2026, 7, 31, 3, 0, 0, TimeSpan.Zero);

        var savedUpdate = await mediator.Send(new SaveDonateActivityCommand(new DonateLotteryActivity
        {
            Id = saved.Id, PublicId = saved.PublicId, Name = saved.Name, MinimumDonationAmount = 100m,
            StartsAtUtc = expectedStart, EndsAtUtc = expectedEnd, Animation = DonateLotteryAnimation.ScratchCard,
            ResultDisplayDurationSeconds = 45, AnimationDurationSeconds = 12, ShowDonateInformation = false
        }));

        Assert.IsFalse(savedUpdate.ShowDonateInformation);

        var updated = (await repository.ListAsync()).Single(activity => activity.Id == saved.Id);
        Assert.AreEqual(expectedStart, updated.StartsAtUtc);
        Assert.AreEqual(expectedEnd, updated.EndsAtUtc);
        Assert.AreEqual(DonateLotteryAnimation.ScratchCard, updated.Animation);
        Assert.AreEqual(45, updated.ResultDisplayDurationSeconds);
        Assert.AreEqual(12, updated.AnimationDurationSeconds);
        Assert.IsFalse(updated.ShowDonateInformation);
    }

    [TestMethod]
    public async Task ListAsync_ReadsDonateActivitiesFromSplitYamlDocument()
    {
        var services = CreateServices();
        var repository = (YamlDonateActivityRepository)services.GetRequiredService<IDonateLotteryActivityRepository>();
        await File.WriteAllTextAsync(repository.SnapshotPath, """
            donateLotteryActivities:
            - id: 7
              publicId: 7b6e3891-53c3-4c6f-8433-75bdbb9b7f38
              name: 拍立得
            """);

        var activities = await repository.ListAsync();

        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual(7, activities[0].Id);
        Assert.AreEqual("拍立得", activities[0].Name);
        Assert.AreEqual(15, activities[0].ResultDisplayDurationSeconds);
        Assert.AreEqual(8, activities[0].AnimationDurationSeconds);
        Assert.IsTrue(activities[0].ShowDonateInformation);
    }

    [TestMethod]
    public void NormalizeForSave_AllowsPastStartButRequiresEndThirtyMinutesAhead()
    {
        var activity = new DonateLotteryActivity
        {
            Name = "活動",
            MinimumDonationAmount = 100m,
            StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            EndsAtUtc = DateTimeOffset.UtcNow.AddMinutes(29)
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => DonateActivityRules.NormalizeForSave(activity, []));

        StringAssert.Contains(exception.Message, "30 分鐘後");
    }

    [TestMethod]
    public void NormalizeForSave_RejectsResultDisplayDurationOutsideAllowedRange()
    {
        var activity = new DonateLotteryActivity
        {
            Name = "活動",
            MinimumDonationAmount = 100m,
            StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            ResultDisplayDurationSeconds = 2
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => DonateActivityRules.NormalizeForSave(activity, []));

        StringAssert.Contains(exception.Message, "3 至 300 秒");
    }

    [TestMethod]
    public void NormalizeForSave_RejectsAnimationDurationOutsideAllowedRange()
    {
        var activity = new DonateLotteryActivity
        {
            Name = "活動", MinimumDonationAmount = 100m,
            StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1), EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            AnimationDurationSeconds = 31
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => DonateActivityRules.NormalizeForSave(activity, []));

        StringAssert.Contains(exception.Message, "3 至 30 秒");
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
        services.AddLogging();
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
