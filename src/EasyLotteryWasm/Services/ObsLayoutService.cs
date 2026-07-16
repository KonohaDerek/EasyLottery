using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryWasm.Models;

namespace EasyLotteryWasm.Services
{
    public sealed class ObsLayoutService
    {
        private readonly IEasyLotteryConfigStore _configStore;
        private string? _activeLayoutKey;

        public ObsLayoutService(IEasyLotteryConfigStore configStore)
        {
            _configStore = configStore;
        }

        public IReadOnlyList<ObsLayoutPreset> Presets => ObsLayoutCatalog.All;

        public async Task<string> GetActiveLayoutKeyAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(_activeLayoutKey))
            {
                return _activeLayoutKey;
            }

            var document = await _configStore.LoadAsync(cancellationToken);
            _activeLayoutKey = ObsLayoutCatalog.NormalizeKey(document.ObsLayout?.ActiveLayoutKey);
            return _activeLayoutKey;
        }

        public ObsLayoutPreset GetPreset(string? key) => ObsLayoutCatalog.GetByKey(key);

        public async Task<ObsLayoutPreset> SetActiveLayoutAsync(string key, CancellationToken cancellationToken = default)
        {
            var normalized = ObsLayoutCatalog.NormalizeKey(key);
            var document = await _configStore.LoadAsync(cancellationToken);
            document.ObsLayout ??= new ObsLayoutSettings();
            document.ObsLayout.ActiveLayoutKey = normalized;
            await _configStore.SaveAsync(document, cancellationToken);
            _activeLayoutKey = normalized;
            return ObsLayoutCatalog.GetByKey(normalized);
        }
    }
}