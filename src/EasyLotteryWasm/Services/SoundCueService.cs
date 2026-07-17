using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryWasm.Models;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services
{
    public sealed class SoundCueService
    {
        private readonly IEasyLotteryConfigStore _configStore;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<SoundCueService> _logger;
        private string? _activePresetKey;

        public SoundCueService(
            IEasyLotteryConfigStore configStore,
            IJSRuntime jsRuntime,
            ILogger<SoundCueService> logger)
        {
            _configStore = configStore;
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

            var document = await _configStore.LoadAsync(cancellationToken);
            _activePresetKey = SoundCuePreset.NormalizeKey(document.SoundCue?.ActivePresetKey);
            return _activePresetKey;
        }

        public SoundCuePreset GetPreset(string? key) => SoundCuePreset.GetByKey(key);

        public async Task<SoundCuePreset> SetActivePresetAsync(string key, CancellationToken cancellationToken = default)
        {
            var normalized = SoundCuePreset.NormalizeKey(key);
            var document = await _configStore.LoadAsync(cancellationToken);
            document.SoundCue ??= new SoundCueSettings();
            document.SoundCue.ActivePresetKey = normalized;
            await _configStore.SaveAsync(document, cancellationToken);
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