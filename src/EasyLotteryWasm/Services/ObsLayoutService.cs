using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryWasm.Models;

namespace EasyLotteryWasm.Services
{
    public sealed class ObsLayoutService
    {
        private readonly SettingsResourceApiClient _settingsApi;
        private string? _activeLayoutKey;
        private string _etag = "";

        public ObsLayoutService(SettingsResourceApiClient settingsApi)
        {
            _settingsApi = settingsApi;
        }

        public IReadOnlyList<ObsLayoutPreset> Presets => ObsLayoutCatalog.All;

        public async Task<string> GetActiveLayoutKeyAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(_activeLayoutKey))
            {
                return _activeLayoutKey;
            }

            var snapshot = await _settingsApi.GetObsLayoutAsync(cancellationToken);
            _etag = snapshot.ETag;
            _activeLayoutKey = ObsLayoutCatalog.NormalizeKey(snapshot.Value.ActiveLayoutKey);
            return _activeLayoutKey;
        }

        public ObsLayoutPreset GetPreset(string? key) => ObsLayoutCatalog.GetByKey(key);

        public async Task<ObsLayoutPreset> SetActiveLayoutAsync(string key, CancellationToken cancellationToken = default)
        {
            var normalized = ObsLayoutCatalog.NormalizeKey(key);
            var snapshot = await _settingsApi.SaveObsLayoutAsync(new ObsLayoutSettings { ActiveLayoutKey = normalized }, _etag, cancellationToken);
            _etag = snapshot.ETag;
            _activeLayoutKey = normalized;
            return ObsLayoutCatalog.GetByKey(normalized);
        }
    }
}
