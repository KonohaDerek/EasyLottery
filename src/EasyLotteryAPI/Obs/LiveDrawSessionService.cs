using System.Collections.Concurrent;
using EasyLotteryDomain.Models;
using EasyLotteryApplication.Templates;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi.Obs;

public sealed class LiveDrawSessionService
{
    public const string StateChangedEvent = "LiveDrawStateChanged";

    private readonly IPokeTemplateRepository _pokeService;
    private readonly IRouletteTemplateRepository _rouletteService;
    private readonly IHubContext<LiveDrawHub> _hub;
    private readonly IReadOnlyDictionary<LiveDrawSessionKind, Func<Guid, Task<int?>>> _templateIdLoaders;
    private readonly ConcurrentDictionary<(LiveDrawSessionKind Kind, Guid PublicId), LiveDrawSessionState> _states = new();
    private readonly ConcurrentDictionary<(LiveDrawSessionKind Kind, Guid PublicId), SemaphoreSlim> _sessionGates = new();
    private readonly SemaphoreSlim _persistenceGate = new(1, 1);

    public LiveDrawSessionService(IPokeTemplateRepository pokeService, IRouletteTemplateRepository rouletteService, IHubContext<LiveDrawHub> hub)
    {
        _pokeService = pokeService;
        _rouletteService = rouletteService;
        _hub = hub;
        _templateIdLoaders = new Dictionary<LiveDrawSessionKind, Func<Guid, Task<int?>>>
        {
            [LiveDrawSessionKind.PokeBox] = async publicId => (await _pokeService.GetByPublicIdAsync(publicId))?.Id,
            [LiveDrawSessionKind.Roulette] = async publicId => (await _rouletteService.GetByPublicIdAsync(publicId))?.Id
        };
    }

    public async Task<LiveDrawSessionState?> GetAsync(LiveDrawSessionKind kind, Guid publicId)
    {
        if (_states.TryGetValue((kind, publicId), out var state))
        {
            return state;
        }

        return await CreateReadyStateAsync(kind, publicId);
    }

    public async Task<LiveDrawSessionState> PokeAsync(Guid publicId, PokeLiveDrawCommand command, CancellationToken cancellationToken)
    {
        var key = (LiveDrawSessionKind.PokeBox, publicId);
        var gate = _sessionGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("此戳戳樂工作階段正在執行中。");
        }

        try
        {
            var template = await _pokeService.GetByPublicIdAsync(publicId)
                ?? throw new KeyNotFoundException("找不到戳戳樂模板。");
            var countdown = Math.Clamp(command.CountdownSeconds, 0, 10);
            if (countdown > 0)
            {
                await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.CountingDown, TimeSpan.FromSeconds(countdown)), cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(countdown), cancellationToken);
            }

            await _persistenceGate.WaitAsync(cancellationToken);
            EasyLotteryDomain.Models.Entities.PokeCell? cell;
            try
            {
                cell = command.CellIndex.HasValue
                    ? await _pokeService.PokeCellAsync(template.Id, command.CellIndex.Value, cancellationToken)
                    : await _pokeService.PokeRandomAsync(template.Id, cancellationToken);
            }
            finally
            {
                _persistenceGate.Release();
            }

            if (cell is null)
            {
                throw new InvalidOperationException("沒有可揭露的格子。");
            }

            var animationDuration = TimeSpan.FromMilliseconds(Math.Clamp(template.AnimationDurationMs, 300, 10000));
            await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.Animating, animationDuration) with
            {
                PokeCellIndex = cell.Index,
                ResultTitle = cell.Title
            }, cancellationToken);
            await Task.Delay(animationDuration, cancellationToken);

            await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.ShowingResult) with
            {
                PokeCellIndex = cell.Index,
                ResultTitle = cell.Title
            }, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(template.ResultDisplayDurationSeconds, 1, 300)), cancellationToken);
            return await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.Ready), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LiveDrawSessionState> SpinAsync(Guid publicId, RouletteLiveDrawCommand command, CancellationToken cancellationToken)
    {
        var key = (LiveDrawSessionKind.Roulette, publicId);
        var gate = _sessionGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("此轉盤工作階段正在執行中。");
        }

        try
        {
            var template = await _rouletteService.GetByPublicIdAsync(publicId)
                ?? throw new KeyNotFoundException("找不到轉盤模板。");
            var countdown = Math.Clamp(command.CountdownSeconds, 0, 10);
            if (countdown > 0)
            {
                await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.CountingDown, TimeSpan.FromSeconds(countdown)), cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(countdown), cancellationToken);
            }

            var currentRotation = _states.TryGetValue(key, out var previous) ? previous.RouletteRotationDeg : 0;
            var result = await _rouletteService.SpinAsync(template.Id, command.ForceIndex, currentRotation, cancellationToken);
            var targetRotation = currentRotation + result.TotalRotationDeg;
            var duration = TimeSpan.FromSeconds(Math.Clamp(template.SpinDurationSec, 0.1, 30));

            await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.Animating, duration) with
            {
                RouletteSegmentIndex = result.SegmentIndex,
                RouletteRotationDeg = targetRotation,
                ResultTitle = result.SegmentTitle
            }, cancellationToken);
            await Task.Delay(duration, cancellationToken);

            await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.ShowingResult) with
            {
                RouletteSegmentIndex = result.SegmentIndex,
                RouletteRotationDeg = targetRotation,
                ResultTitle = result.SegmentTitle
            }, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(template.ResultDisplayDurationSeconds, 1, 300)), cancellationToken);
            return await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.Ready) with
            {
                RouletteRotationDeg = targetRotation
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LiveDrawSessionState> ResetPokeAsync(Guid publicId, CancellationToken cancellationToken)
    {
            var template = await _pokeService.GetByPublicIdAsync(publicId)
            ?? throw new KeyNotFoundException("找不到戳戳樂模板。");
        await _persistenceGate.WaitAsync(cancellationToken);
        try
        {
            await _pokeService.ResetAsync(template.Id, cancellationToken);
        }
        finally
        {
            _persistenceGate.Release();
        }

        return await PublishAsync(NewState((LiveDrawSessionKind.PokeBox, publicId), template.Id, LiveDrawSessionPhase.Ready), cancellationToken);
    }

    private async Task<LiveDrawSessionState?> CreateReadyStateAsync(LiveDrawSessionKind kind, Guid publicId)
    {
        if (!_templateIdLoaders.TryGetValue(kind, out var loadTemplateId)) return null;
        var templateId = await loadTemplateId(publicId);
        if (!templateId.HasValue)
        {
            return null;
        }

        return _states.GetOrAdd((kind, publicId), key => NewState(key, templateId.Value, LiveDrawSessionPhase.Ready));
    }

    private LiveDrawSessionState NewState(
        (LiveDrawSessionKind Kind, Guid PublicId) key,
        int templateId,
        LiveDrawSessionPhase phase,
        TimeSpan? duration = null)
    {
        var revision = _states.TryGetValue(key, out var previous) ? previous.Revision + 1 : 1;
        var now = DateTimeOffset.UtcNow;
        return new LiveDrawSessionState
        {
            Kind = key.Kind,
            PublicId = key.PublicId,
            TemplateId = templateId,
            Revision = revision,
            Phase = phase,
            UpdatedAtUtc = now,
            PhaseEndsAtUtc = duration.HasValue ? now.Add(duration.Value) : null,
            RouletteRotationDeg = previous?.RouletteRotationDeg ?? 0
        };
    }

    private async Task<LiveDrawSessionState> PublishAsync(LiveDrawSessionState state, CancellationToken cancellationToken)
    {
        _states[(state.Kind, state.PublicId)] = state;
        await _hub.Clients.Group(LiveDrawSessionGroups.For(state.Kind, state.PublicId))
            .SendAsync(StateChangedEvent, state, cancellationToken);
        return state;
    }
}
