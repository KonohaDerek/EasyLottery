using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryWasm.Models;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services
{
    public sealed class SoundCueService
    {
        private readonly SettingsResourceApiClient _settingsApi;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<SoundCueService> _logger;
        private string? _activePresetKey;
        private string _etag = "";

        public SoundCueService(
            SettingsResourceApiClient settingsApi,
            IJSRuntime jsRuntime,
            ILogger<SoundCueService> logger)
        {
            _settingsApi = settingsApi;
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public IReadOnlyList<SoundCuePreset> Presets => SoundCuePreset.Catalog;

        public async Task<string> GetActivePresetKeyAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(_activePresetKey))
            {
                return _activePresetKey;
            }

            var snapshot = await _settingsApi.GetSoundCueAsync(cancellationToken);
            _etag = snapshot.ETag;
            _activePresetKey = SoundCuePreset.NormalizeKey(snapshot.Value.ActivePresetKey);
            return _activePresetKey;
        }

        public SoundCuePreset GetPreset(string? key) => SoundCuePreset.GetByKey(key);

        public async Task<SoundCuePreset> SetActivePresetAsync(string key, CancellationToken cancellationToken = default)
        {
            var normalized = SoundCuePreset.NormalizeKey(key);
            var snapshot = await _settingsApi.SaveSoundCueAsync(new SoundCueSettings { ActivePresetKey = normalized }, _etag, cancellationToken);
            _etag = snapshot.ETag;
            _activePresetKey = normalized;
            return SoundCuePreset.GetByKey(normalized);
        }

        public async Task PlayCueAsync(string cue, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cue))
            {
                return;
            }

            try
            {
                await _jsRuntime.InvokeVoidAsync("easyLotteryAudio.playCue", cancellationToken, cue);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Failed to play sound cue '{Cue}'.", cue);
            }
        }
    }
}
