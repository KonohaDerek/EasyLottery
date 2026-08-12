using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryApi.Security;

namespace EasyLotteryApi.Settings;

public sealed class ObsSettingsProjectionService
{
    private readonly IEasyLotteryConfigStore _store;

    public ObsSettingsProjectionService(IEasyLotteryConfigStore store) => _store = store;

    public async Task<string?> ReadAsync(ObsResourceKind kind, string resourceId, CancellationToken cancellationToken)
    {
        var source = await _store.LoadAsync(cancellationToken);
        var projection = new EasyLotteryConfigDocument
        {
            ObsLayout = source.ObsLayout,
            VisualStyle = source.VisualStyle,
            SoundCue = source.SoundCue
        };

        var publicId = Guid.TryParse(resourceId, out var parsed) ? parsed : Guid.Empty;
        var projectors = new Dictionary<ObsResourceKind, Func<bool>>
        {
            [ObsResourceKind.Donate] = () =>
            {
                var activity = source.DonateLotteryActivities.FirstOrDefault(item => item.PublicId == publicId);
                if (activity is null) return false;
                projection.DonateLotteryActivities.Add(activity);
                projection.DonateLotteryDrawRecords.AddRange(source.DonateLotteryDrawRecords.Where(item => item.ActivityId == activity.Id));
                return true;
            },
            [ObsResourceKind.PokeBox] = () =>
            {
                var poke = source.PokeTemplates.FirstOrDefault(item => item.PublicId == publicId);
                if (poke is null) return false;
                projection.PokeTemplates.Add(poke);
                return true;
            },
            [ObsResourceKind.Roulette] = () =>
            {
                var roulette = source.RouletteTemplates.FirstOrDefault(item => item.PublicId == publicId);
                if (roulette is null) return false;
                projection.RouletteTemplates.Add(roulette);
                return true;
            },
            [ObsResourceKind.Overtime] = () =>
            {
                projection.OvertimeOverlay = source.OvertimeOverlay;
                return true;
            }
        };

        if (!projectors.TryGetValue(kind, out var project) || !project())
        {
            return null;
        }

        return YamlSerialization.CreateSerializerBuilder().Build().Serialize(projection);
    }

    public async Task UpdateResourceAsync(ObsResourceKind kind, string resourceId, string yaml, CancellationToken cancellationToken)
    {
        var incoming = YamlSerialization.CreateDeserializerBuilder().Build().Deserialize<EasyLotteryConfigDocument>(yaml)
            ?? throw new InvalidOperationException("無法解析 OBS 資源設定。");
        var document = await _store.LoadAsync(cancellationToken);
        var publicId = Guid.TryParse(resourceId, out var parsed) ? parsed : Guid.Empty;
        var updaters = new Dictionary<ObsResourceKind, Action>
        {
            [ObsResourceKind.Overtime] = () =>
            {
                document.OvertimeOverlay = incoming.OvertimeOverlay ?? new OvertimeOverlaySettings();
            },
            [ObsResourceKind.PokeBox] = () =>
            {
                var targetPoke = document.PokeTemplates.FirstOrDefault(item => item.PublicId == publicId)
                    ?? throw new InvalidOperationException("找不到 scoped 戳戳樂模板。");
                var incomingPoke = incoming.PokeTemplates.FirstOrDefault(item => item.PublicId == publicId)
                    ?? throw new InvalidOperationException("OBS 更新未包含 scoped 戳戳樂模板。");
                foreach (var targetCell in targetPoke.Cells)
                {
                    var incomingCell = incomingPoke.Cells.FirstOrDefault(item => item.Index == targetCell.Index);
                    if (incomingCell is null) continue;
                    targetCell.IsRevealed = incomingCell.IsRevealed;
                    targetCell.RevealedAt = incomingCell.RevealedAt;
                }
                AppendResults(document, incoming, ActivityResultType.PokeBox, targetPoke.Id);
            },
            [ObsResourceKind.Roulette] = () =>
            {
                var targetRoulette = document.RouletteTemplates.FirstOrDefault(item => item.PublicId == publicId)
                    ?? throw new InvalidOperationException("找不到 scoped 轉盤模板。");
                AppendResults(document, incoming, ActivityResultType.Roulette, targetRoulette.Id);
            }
        };

        if (!updaters.TryGetValue(kind, out var update))
        {
            throw new InvalidOperationException("此 OBS 資源不可更新 settings。");
        }
        update();
        await _store.SaveAsync(document, cancellationToken);
    }

    private static void AppendResults(EasyLotteryConfigDocument target, EasyLotteryConfigDocument incoming, ActivityResultType type, int templateId)
    {
        foreach (var result in incoming.ActivityResults.Where(item => item.ActivityType == type && item.TemplateId == templateId))
        {
            result.Id = target.IdSequence.NextActivityResultId++;
            target.ActivityResults.Add(result);
        }
    }
}
