using EasyLotteryApplication.DonateActivities;
using EasyLotteryApplication.Payments;
using EasyLotteryInfrastructure.DonateActivities;
using EasyLotteryInfrastructure.Payments;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryInfrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEasyLotteryInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IStorageGateProvider, StorageGateProvider>();
        services.AddSingleton<YamlDonateActivityEventStore>();
        services.AddSingleton<IDonateActivityEventStore>(sp => sp.GetRequiredService<YamlDonateActivityEventStore>());
        services.AddSingleton<YamlDonateActivityRepository>();
        services.AddSingleton<IDonateLotteryActivityRepository>(sp => sp.GetRequiredService<YamlDonateActivityRepository>());
        services.AddSingleton<JsonPaymentEventRepository>();
        services.AddSingleton<IPaymentEventRepository>(sp => sp.GetRequiredService<JsonPaymentEventRepository>());
        services.AddSingleton<JsonPaymentOrderRepository>();
        services.AddSingleton<IPaymentOrderRepository>(sp => sp.GetRequiredService<JsonPaymentOrderRepository>());
        services.AddSingleton<JsonOvertimeFeedRepository>();
        services.AddSingleton<IOvertimeFeedRepository>(sp => sp.GetRequiredService<JsonOvertimeFeedRepository>());
        return services;
    }
}
