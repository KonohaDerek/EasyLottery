using System.Collections.Generic;
using System.Linq;

namespace EasyLotteryWasm.Models
{
    public sealed class SoundCuePreset
    {
        public string Key { get; init; } = "";

        public string Name { get; init; } = "";

        public string Description { get; init; } = "";

        public string CountdownCue { get; init; } = "";

        public string SpinCue { get; init; } = "";

        public string PokeCue { get; init; } = "";

        public string RevealCue { get; init; } = "";

        public string ResultCue { get; init; } = "";

        public static IReadOnlyList<SoundCuePreset> Catalog { get; } = new[]
        {
            new SoundCuePreset
            {
                Key = "arcade-stage",
                Name = "街機舞台",
                Description = "偏明亮的短促提示音，適合節奏較快的直播。",
                CountdownCue = "tone:784:140:square",
                SpinCue = "tone:988:180:triangle",
                PokeCue = "tone:659:120:sine",
                RevealCue = "tone:932:180:sawtooth",
                ResultCue = "tone:1046:240:triangle"
            },
            new SoundCuePreset
            {
                Key = "soft-stage",
                Name = "柔和舞台",
                Description = "較溫和的提示音，適合長時間直播或較安靜的場景。",
                CountdownCue = "tone:523:160:sine",
                SpinCue = "tone:659:220:triangle",
                PokeCue = "tone:440:120:sine",
                RevealCue = "tone:587:200:sine",
                ResultCue = "tone:740:260:triangle"
            },
            new SoundCuePreset
            {
                Key = "retro-pixel",
                Name = "復古像素",
                Description = "顆粒感更強的音色，適合點陣 HUD 與遊戲化版型。",
                CountdownCue = "tone:880:100:square",
                SpinCue = "tone:1175:160:square",
                PokeCue = "tone:740:90:square",
                RevealCue = "tone:988:140:sawtooth",
                ResultCue = "tone:1319:220:triangle"
            }
        };

        public static SoundCuePreset GetByKey(string? key)
        {
            var normalizedKey = NormalizeKey(key);
            return Catalog.FirstOrDefault(item => string.Equals(item.Key, normalizedKey, System.StringComparison.OrdinalIgnoreCase)) ?? Catalog[0];
        }

        public static string NormalizeKey(string? key)
        {
            return Catalog.Any(item => string.Equals(item.Key, key, System.StringComparison.OrdinalIgnoreCase))
                ? key!.Trim()
                : Catalog[0].Key;
        }
    }
}