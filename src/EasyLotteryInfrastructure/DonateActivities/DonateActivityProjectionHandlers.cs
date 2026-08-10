using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Services;
using MediatR;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.DonateActivities;

public sealed class DonateActivityProjectionHandlers :
    INotificationHandler<DonateActivitySavedNotification>,
    INotificationHandler<DonateActivityDeletedNotification>
{
    private readonly YamlDonateActivityRepository _repository;
    private readonly YamlDonateActivityEventStore _eventStore;
    private readonly IStorageGateProvider _storageGates;
    private readonly IEasyLotteryConfigStore _configStore;
    private readonly IOptions<StorageProviderOptions> _options;

    public DonateActivityProjectionHandlers(
        YamlDonateActivityRepository repository,
        YamlDonateActivityEventStore eventStore,
        IStorageGateProvider storageGates,
        IEasyLotteryConfigStore configStore,
        IOptions<StorageProviderOptions> options)
    {
        _repository = repository;
        _eventStore = eventStore;
        _storageGates = storageGates;
        _configStore = configStore;
        _options = options;
    }

    public async Task Handle(DonateActivitySavedNotification notification, CancellationToken cancellationToken)
    {
        if (_options.Value.NormalizedProvider == "sqlite")
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            document.DonateLotteryActivities.RemoveAll(activity => activity.Id == notification.Activity.Id);
            document.DonateLotteryActivities.Add(Clone(notification.Activity));
            await _configStore.SaveAsync(document, cancellationToken);
            return;
        }

        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _repository.SnapshotPath, _eventStore.EventLogPath);
        var events = (await _eventStore.ReadAllUnsafeAsync(cancellationToken)).ToList();
        events.Add(new DonateActivityEventRecord
        {
            Type = DonateActivityEventTypes.Saved,
            ActivityId = notification.Activity.Id,
            Activity = Clone(notification.Activity),
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await _eventStore.AppendUnsafeAsync(events[^1], cancellationToken);
        var activities = DonateActivityEventProjector.Rebuild(events);
        await _repository.WriteSnapshotUnsafeAsync(activities, cancellationToken);
    }

    public async Task Handle(DonateActivityDeletedNotification notification, CancellationToken cancellationToken)
    {
        if (_options.Value.NormalizedProvider == "sqlite")
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            document.DonateLotteryActivities.RemoveAll(activity => activity.Id == notification.ActivityId);
            await _configStore.SaveAsync(document, cancellationToken);
            return;
        }

        await using var gate = await _storageGates.AcquireAsync(cancellationToken, _repository.SnapshotPath, _eventStore.EventLogPath);
        var events = (await _eventStore.ReadAllUnsafeAsync(cancellationToken)).ToList();
        events.Add(new DonateActivityEventRecord
        {
            Type = DonateActivityEventTypes.Deleted,
            ActivityId = notification.ActivityId,
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await _eventStore.AppendUnsafeAsync(events[^1], cancellationToken);
        var activities = DonateActivityEventProjector.Rebuild(events);
        await _repository.WriteSnapshotUnsafeAsync(activities, cancellationToken);
    }

    private static EasyLotteryDomain.Models.Config.DonateLotteryActivity Clone(EasyLotteryDomain.Models.Config.DonateLotteryActivity source) =>
        new()
        {
            Id = source.Id,
            PublicId = source.PublicId,
            Name = source.Name,
            Type = source.Type,
            MinimumDonationAmount = source.MinimumDonationAmount,
            StartsAtUtc = source.StartsAtUtc,
            EndsAtUtc = source.EndsAtUtc,
            Animation = source.Animation,
            PolaroidTemplateKey = source.PolaroidTemplateKey,
            UseAiCongratulation = source.UseAiCongratulation,
            ShowDonateInformation = source.ShowDonateInformation,
            ResultDisplayDurationSeconds = source.ResultDisplayDurationSeconds,
            AnimationDurationSeconds = source.AnimationDurationSeconds,
            UseWebmAnimation = source.UseWebmAnimation,
            WebmAnimationUrl = source.WebmAnimationUrl,
            WebmPosterUrl = source.WebmPosterUrl,
            WebmAnimationLoop = source.WebmAnimationLoop,
            IsEnabled = source.IsEnabled,
            Prizes = source.Prizes.Select(prize => new EasyLotteryDomain.Models.Config.DonateLotteryPrize
            {
                Id = prize.Id,
                Name = prize.Name,
                ImageUrl = prize.ImageUrl,
                Quantity = prize.Quantity,
                RemainingQuantity = prize.RemainingQuantity,
                Probability = prize.Probability,
                IsGrandPrize = prize.IsGrandPrize
            }).ToList()
        };
}
