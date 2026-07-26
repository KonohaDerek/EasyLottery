using EasyLotteryDomain.Models.Config;
using MediatR;

namespace EasyLotteryApplication.DonateActivities;

public sealed record GetDonateActivitiesQuery : IRequest<IReadOnlyList<DonateLotteryActivity>>;

public sealed record GetDonateActivityByIdQuery(int ActivityId) : IRequest<DonateLotteryActivity?>;

public sealed record SaveDonateActivityCommand(DonateLotteryActivity Activity) : IRequest<DonateLotteryActivity>;

public sealed record DeleteDonateActivityCommand(int ActivityId) : IRequest<Unit>;
