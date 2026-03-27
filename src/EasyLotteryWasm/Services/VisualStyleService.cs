using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using Microsoft.JSInterop;
using EasyLotteryWasm.Models;

namespace EasyLotteryWasm.Services
{
    public sealed class VisualStyleService
    {
        private readonly IEasyLotteryConfigStore _configStore;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<VisualStyleService> _logger;
        private bool _initialized;
        private string? _activeThemeKey;

        public VisualStyleService(
            IEasyLotteryConfigStore configStore,
            IJSRuntime jsRuntime,
            ILogger<VisualStyleService> logger)
        {
            _configStore = configStore;
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public IReadOnlyList<VisualStylePreset> Presets => VisualStyleCatalog.All;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            var document = await _configStore.LoadAsync(cancellationToken);
            var preset = VisualStyleCatalog.GetByKey(document.VisualStyle?.ActiveThemeKey);
            _activeThemeKey = preset.Key;
            await ApplyThemeAsync(preset.Key, cancellationToken);
            _initialized = true;
        }

        public async Task<string> GetActiveThemeKeyAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(_activeThemeKey))
            {
                return _activeThemeKey;
            }

            var document = await _configStore.LoadAsync(cancellationToken);
            _activeThemeKey = VisualStyleCatalog.NormalizeKey(document.VisualStyle?.ActiveThemeKey);
            return _activeThemeKey;
        }

        public VisualStylePreset GetPreset(string? key) => VisualStyleCatalog.GetByKey(key);

        public async Task<VisualStylePreset> SetActiveThemeAsync(string key, CancellationToken cancellationToken = default)
        {
            var normalized = VisualStyleCatalog.NormalizeKey(key);
            var document = await _configStore.LoadAsync(cancellationToken);
            document.VisualStyle ??= new VisualStyleSettings();
            document.VisualStyle.ActiveThemeKey = normalized;
            await _configStore.SaveAsync(document, cancellationToken);
            _activeThemeKey = normalized;
            await ApplyThemeAsync(normalized, cancellationToken);
            return VisualStyleCatalog.GetByKey(normalized);
        }

        private async Task ApplyThemeAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("easyLotteryTheme.apply", cancellationToken, key);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Failed to apply visual style '{ThemeKey}' through the browser bridge.", key);
            }
        }
    }
}
