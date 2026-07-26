using EasyLotteryApplication.DonateActivities;
using EasyLotteryInfrastructure.DonateActivities;
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
        return services;
    }
}
