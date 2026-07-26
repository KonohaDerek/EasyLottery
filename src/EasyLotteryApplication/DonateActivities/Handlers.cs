using EasyLotteryDomain.Models.Config;
using MediatR;

namespace EasyLotteryApplication.DonateActivities;

public sealed class GetDonateActivitiesQueryHandler : IRequestHandler<GetDonateActivitiesQuery, IReadOnlyList<DonateLotteryActivity>>
{
    private readonly IDonateLotteryActivityRepository _repository;

    public GetDonateActivitiesQueryHandler(IDonateLotteryActivityRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<DonateLotteryActivity>> Handle(GetDonateActivitiesQuery request, CancellationToken cancellationToken) =>
        (await _repository.ListAsync(cancellationToken)).OrderByDescending(activity => activity.Id).ToList();
}

public sealed class GetDonateActivityByIdQueryHandler : IRequestHandler<GetDonateActivityByIdQuery, DonateLotteryActivity?>
{
    private readonly IDonateLotteryActivityRepository _repository;

    public GetDonateActivityByIdQueryHandler(IDonateLotteryActivityRepository repository) => _repository = repository;

    public Task<DonateLotteryActivity?> Handle(GetDonateActivityByIdQuery request, CancellationToken cancellationToken) =>
        _repository.GetByIdAsync(request.ActivityId, cancellationToken);
}

public sealed class SaveDonateActivityCommandHandler : IRequestHandler<SaveDonateActivityCommand, DonateLotteryActivity>
{
    private readonly IDonateLotteryActivityRepository _repository;
    private readonly IPublisher _publisher;

    public SaveDonateActivityCommandHandler(IDonateLotteryActivityRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<DonateLotteryActivity> Handle(SaveDonateActivityCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.ListAsync(cancellationToken);
        var saved = DonateActivityRules.NormalizeForSave(request.Activity, existing);
        await _publisher.Publish(new DonateActivitySavedNotification(saved), cancellationToken);
        return saved;
    }
}

public sealed class DeleteDonateActivityCommandHandler : IRequestHandler<DeleteDonateActivityCommand, Unit>
{
    private readonly IDonateLotteryActivityRepository _repository;
    private readonly IPublisher _publisher;

    public DeleteDonateActivityCommandHandler(IDonateLotteryActivityRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(DeleteDonateActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await _repository.GetByIdAsync(request.ActivityId, cancellationToken);
        if (activity is null)
        {
            throw new InvalidOperationException("找不到 Donate 活動。");
        }

        await _publisher.Publish(new DonateActivityDeletedNotification(request.ActivityId), cancellationToken);
        return Unit.Value;
    }
}
