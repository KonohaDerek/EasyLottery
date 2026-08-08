using EasyLotteryApplication.DonateActivities;
using EasyLotteryApplication.Payments;
using EasyLotteryApplication.Settings;
using EasyLotteryApplication.Templates;
using EasyLotteryApplication.ObsAssets;
using EasyLotteryInfrastructure.DonateActivities;
using EasyLotteryInfrastructure.Payments;
using EasyLotteryInfrastructure.Settings;
using EasyLotteryInfrastructure.Storage;
using EasyLotteryInfrastructure.Templates;
using EasyLotteryInfrastructure.ObsAssets;
using EasyLotteryDomain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEasyLotteryInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<StorageProviderOptions>()
            .BindConfiguration(StorageProviderOptions.SectionName)
            .Validate(options => { options.Validate(); options.ValidateAdapterAvailability(); return true; }, "Storage provider 設定無效")
            .ValidateOnStart();
        services.AddHostedService<SqliteSchemaMigrator>();
        services.AddSingleton<IStorageGateProvider, StorageGateProvider>();
        services.AddSingleton<YamlDonateActivityEventStore>();
        services.AddSingleton<IDonateActivityEventStore>(sp => sp.GetRequiredService<YamlDonateActivityEventStore>());
        services.AddSingleton<YamlDonateActivityRepository>();
        services.AddSingleton<SqliteDonateActivityRepository>();
        services.AddSingleton<IDonateLotteryActivityRepository>(sp =>
        {
            var provider = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageProviderOptions>>().Value.NormalizedProvider;
            return provider == "sqlite"
                ? sp.GetRequiredService<SqliteDonateActivityRepository>()
                : sp.GetRequiredService<YamlDonateActivityRepository>();
        });
        services.AddSingleton<JsonPaymentEventRepository>();
        services.AddSingleton<IPaymentEventRepository>(sp => sp.GetRequiredService<JsonPaymentEventRepository>());
        services.AddSingleton<JsonPaymentOrderRepository>();
        services.AddSingleton<IPaymentOrderRepository>(sp => sp.GetRequiredService<JsonPaymentOrderRepository>());
        services.AddSingleton<JsonOvertimeFeedRepository>();
        services.AddSingleton<IOvertimeFeedRepository>(sp => sp.GetRequiredService<JsonOvertimeFeedRepository>());
        services.AddSingleton<YamlSettingsDocumentRepository>();
        services.AddSingleton<ISettingsYamlDocumentRepository>(sp => sp.GetRequiredService<YamlSettingsDocumentRepository>());
        services.AddSingleton<YamlActivitiesDocumentRepository>();
        services.AddSingleton<IActivitiesYamlDocumentRepository>(sp => sp.GetRequiredService<YamlActivitiesDocumentRepository>());
        services.AddSingleton<YamlActivityResultsDocumentRepository>();
        services.AddSingleton<IActivityResultsYamlDocumentRepository>(sp => sp.GetRequiredService<YamlActivityResultsDocumentRepository>());
        services.AddSingleton<ConfigSecretRedactor>();
        services.AddSingleton<SettingsSectionRules>();
        services.AddSingleton<YamlSettingsSectionRepository>();
        services.AddSingleton<ISettingsSectionRepository>(sp => sp.GetRequiredService<YamlSettingsSectionRepository>());
        services.AddSingleton<SettingsFileStore>();
        services.AddSingleton<SqliteConfigStore>();
        services.AddSingleton<IEasyLotteryConfigRepository>(sp => sp.GetRequiredService<SettingsFileStore>());
        services.AddSingleton<IEasyLotteryConfigStore>(sp =>
        {
            var provider = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageProviderOptions>>().Value.NormalizedProvider;
            return provider == "sqlite"
                ? sp.GetRequiredService<SqliteConfigStore>()
                : sp.GetRequiredService<SettingsFileStore>();
        });
        services.AddSingleton<ActivityResultService>();
        services.AddSingleton<IPokeTemplateRepository, PokeTemplateRepository>();
        services.AddSingleton<IRouletteTemplateRepository, RouletteTemplateRepository>();
        services.AddSingleton<IActivityResultRepository, ActivityResultRepository>();
        services.AddSingleton<YamlObsAssetRepository>();
        services.AddSingleton<IObsAssetRepository>(sp => sp.GetRequiredService<YamlObsAssetRepository>());
        return services;
    }
}
