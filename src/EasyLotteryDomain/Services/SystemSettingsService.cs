using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services
{
    public class SystemSettingsService
    {
        private readonly IEasyLotteryConfigStore _configStore;

        public SystemSettingsService(IEasyLotteryConfigStore configStore)
        {
            _configStore = configStore;
        }

        public async Task<LotterySystemSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            return document.SystemSettings;
        }

        public async Task<YouTubeApiSettings> GetYouTubeSettingsAsync(CancellationToken cancellationToken = default)
        {
            var settings = await GetSettingsAsync(cancellationToken);
            return settings.YouTube;
        }

        public async Task SaveSettingsAsync(LotterySystemSettings settings, CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            document.SystemSettings = settings ?? new LotterySystemSettings();
            await _configStore.SaveAsync(document, cancellationToken);
        }

        public async Task UpdateYouTubeSettingsAsync(Action<YouTubeApiSettings> updateAction, CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            updateAction(document.SystemSettings.YouTube);
            await _configStore.SaveAsync(document, cancellationToken);
        }
    }
}