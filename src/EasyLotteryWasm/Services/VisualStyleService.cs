using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using Microsoft.JSInterop;
using EasyLotteryWasm.Models;

namespace EasyLotteryWasm.Services
{
    public sealed class VisualStyleService
    {
        private readonly SettingsResourceApiClient _settingsApi;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<VisualStyleService> _logger;
        private bool _initialized;
        private string? _activeThemeKey;
        private string _etag = "";

        public VisualStyleService(
            SettingsResourceApiClient settingsApi,
            IJSRuntime jsRuntime,
            ILogger<VisualStyleService> logger)
        {
            _settingsApi = settingsApi;
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

            var snapshot = await _settingsApi.GetVisualStyleAsync(cancellationToken);
            _etag = snapshot.ETag;
            var preset = VisualStyleCatalog.GetByKey(snapshot.Value.ActiveThemeKey);
            _activeThemeKey = preset.Key;
            await ApplyThemeAsync(preset.Key, snapshot.Value, cancellationToken);
            _initialized = true;
        }

        public async Task<string> GetActiveThemeKeyAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(_activeThemeKey))
            {
                return _activeThemeKey;
            }

            var snapshot = await _settingsApi.GetVisualStyleAsync(cancellationToken);
            _etag = snapshot.ETag;
            _activeThemeKey = VisualStyleCatalog.NormalizeKey(snapshot.Value.ActiveThemeKey);
            return _activeThemeKey;
        }

        public VisualStylePreset GetPreset(string? key) => VisualStyleCatalog.GetByKey(key);

        public async Task<VisualStylePreset> SetActiveThemeAsync(string key, CancellationToken cancellationToken = default)
        {
            var normalized = VisualStyleCatalog.NormalizeKey(key);
            var current = await _settingsApi.GetVisualStyleAsync(cancellationToken);
            var value = new VisualStyleSettings
            {
                ActiveThemeKey = normalized,
                BackgroundImageUrl = current.Value.BackgroundImageUrl,
                BannerImageUrl = current.Value.BannerImageUrl
            };
            var snapshot = await _settingsApi.SaveVisualStyleAsync(value, _etag, cancellationToken);
            _etag = snapshot.ETag;
            _activeThemeKey = normalized;
            await ApplyThemeAsync(normalized, snapshot.Value, cancellationToken);
            return VisualStyleCatalog.GetByKey(normalized);
        }

        public async Task<VisualStyleSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = await _settingsApi.GetVisualStyleAsync(cancellationToken);
            _etag = snapshot.ETag;
            return snapshot.Value;
        }

        public async Task<VisualStyleSettings> SaveImagesAsync(
            string? backgroundImageUrl,
            string? bannerImageUrl,
            CancellationToken cancellationToken = default)
        {
            var current = await _settingsApi.GetVisualStyleAsync(cancellationToken);
            var value = current.Value;
            value.BackgroundImageUrl = backgroundImageUrl ?? "";
            value.BannerImageUrl = bannerImageUrl ?? "";
            var snapshot = await _settingsApi.SaveVisualStyleAsync(value, _etag, cancellationToken);
            _etag = snapshot.ETag;
            await ApplyThemeAsync(snapshot.Value.ActiveThemeKey, snapshot.Value, cancellationToken);
            return snapshot.Value;
        }

        private async Task ApplyThemeAsync(string key, VisualStyleSettings? settings, CancellationToken cancellationToken)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("easyLotteryTheme.apply", cancellationToken, key);
                await _jsRuntime.InvokeVoidAsync(
                    "easyLotteryTheme.applyImages",
                    cancellationToken,
                    settings?.BackgroundImageUrl ?? "",
                    settings?.BannerImageUrl ?? "");
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Failed to apply visual style '{ThemeKey}' through the browser bridge.", key);
            }
        }
    }
}
