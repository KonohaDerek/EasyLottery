using EasyLotteryDomain.Models.Config;
using MediatR;

namespace EasyLotteryApplication.DonateActivities;

public sealed record DonateActivitySavedNotification(DonateLotteryActivity Activity) : INotification;

public sealed record DonateActivityDeletedNotification(int ActivityId) : INotification;
