using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services
{
    public class SystemSettingsService
    {
        private readonly IEasyLotteryConfigStore _configStore;
        private readonly EasyLotteryAuditService? _auditService;

        public SystemSettingsService(IEasyLotteryConfigStore configStore, EasyLotteryAuditService? auditService = null)
        {
            _configStore = configStore;
            _auditService = auditService;
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
            var beforeJson = EasyLotteryAuditService.Snapshot(document.SystemSettings);
            document.SystemSettings = settings ?? new LotterySystemSettings();
            await _configStore.SaveAsync(document, cancellationToken);
            if (_auditService != null)
            {
                await _auditService.RecordAsync("SystemSettings", "更新系統設定", "系統設定", changedBy: document.SystemSettings.Audit.ActorName, beforeJson: beforeJson, afterJson: EasyLotteryAuditService.Snapshot(document.SystemSettings), cancellationToken: cancellationToken);
            }
        }

        public async Task UpdateYouTubeSettingsAsync(Action<YouTubeApiSettings> updateAction, CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            var beforeJson = EasyLotteryAuditService.Snapshot(document.SystemSettings.YouTube);
            updateAction(document.SystemSettings.YouTube);
            await _configStore.SaveAsync(document, cancellationToken);
            if (_auditService != null)
            {
                await _auditService.RecordAsync("SystemSettings", "更新 YouTube 設定", "YouTube", changedBy: document.SystemSettings.Audit.ActorName, beforeJson: beforeJson, afterJson: EasyLotteryAuditService.Snapshot(document.SystemSettings.YouTube), cancellationToken: cancellationToken);
            }
        }
    }
}
