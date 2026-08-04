using System.Collections.Concurrent;
using EasyLotteryDomain.Models;
using EasyLotteryDomain.Services;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi;

public sealed class LiveDrawSessionService
{
    public const string StateChangedEvent = "LiveDrawStateChanged";

    private readonly PokeService _pokeService;
    private readonly RouletteService _rouletteService;
    private readonly IHubContext<LiveDrawHub> _hub;
    private readonly ConcurrentDictionary<(LiveDrawSessionKind Kind, Guid PublicId), LiveDrawSessionState> _states = new();
    private readonly ConcurrentDictionary<(LiveDrawSessionKind Kind, Guid PublicId), SemaphoreSlim> _sessionGates = new();
    private readonly SemaphoreSlim _persistenceGate = new(1, 1);

    public LiveDrawSessionService(PokeService pokeService, RouletteService rouletteService, IHubContext<LiveDrawHub> hub)
    {
        _pokeService = pokeService;
        _rouletteService = rouletteService;
        _hub = hub;
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
            var template = await _pokeService.LoadTemplateAsync(publicId)
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
                    ? await _pokeService.PokeCellByIndexAsync(template.Id, command.CellIndex.Value)
                    : await _pokeService.PokeRandomCellAsync(template.Id);
            }
            finally
            {
                _persistenceGate.Release();
            }

            if (cell is null)
            {
                throw new InvalidOperationException("沒有可揭露的格子。");
            }

            await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.Animating, TimeSpan.FromMilliseconds(650)) with
            {
                PokeCellIndex = cell.Index,
                ResultTitle = cell.Title
            }, cancellationToken);
            await Task.Delay(650, cancellationToken);

            return await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.ShowingResult) with
            {
                PokeCellIndex = cell.Index,
                ResultTitle = cell.Title
            }, cancellationToken);
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
            var template = await _rouletteService.LoadTemplateAsync(publicId)
                ?? throw new KeyNotFoundException("找不到轉盤模板。");
            var countdown = Math.Clamp(command.CountdownSeconds, 0, 10);
            if (countdown > 0)
            {
                await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.CountingDown, TimeSpan.FromSeconds(countdown)), cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(countdown), cancellationToken);
            }

            var currentRotation = _states.TryGetValue(key, out var previous) ? previous.RouletteRotationDeg : 0;
            var result = RouletteService.Spin(template, command.ForceIndex, currentRotation);
            var targetRotation = currentRotation + result.TotalRotationDeg;
            var duration = TimeSpan.FromSeconds(Math.Clamp(template.SpinDurationSec, 0.1, 30));

            await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.Animating, duration) with
            {
                RouletteSegmentIndex = result.SegmentIndex,
                RouletteRotationDeg = targetRotation,
                ResultTitle = result.SegmentTitle
            }, cancellationToken);
            await Task.Delay(duration, cancellationToken);

            return await PublishAsync(NewState(key, template.Id, LiveDrawSessionPhase.ShowingResult) with
            {
                RouletteSegmentIndex = result.SegmentIndex,
                RouletteRotationDeg = targetRotation,
                ResultTitle = result.SegmentTitle
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LiveDrawSessionState> ResetPokeAsync(Guid publicId, CancellationToken cancellationToken)
    {
        var template = await _pokeService.LoadTemplateAsync(publicId)
            ?? throw new KeyNotFoundException("找不到戳戳樂模板。");
        await _persistenceGate.WaitAsync(cancellationToken);
        try
        {
            await _pokeService.ResetTemplateAsync(template.Id);
        }
        finally
        {
            _persistenceGate.Release();
        }

        return await PublishAsync(NewState((LiveDrawSessionKind.PokeBox, publicId), template.Id, LiveDrawSessionPhase.Ready), cancellationToken);
    }

    private async Task<LiveDrawSessionState?> CreateReadyStateAsync(LiveDrawSessionKind kind, Guid publicId)
    {
        var templateId = kind switch
        {
            LiveDrawSessionKind.PokeBox => (await _pokeService.LoadTemplateAsync(publicId))?.Id,
            LiveDrawSessionKind.Roulette => (await _rouletteService.LoadTemplateAsync(publicId))?.Id,
            _ => null
        };
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
        var kind = state.Kind == LiveDrawSessionKind.PokeBox ? "pokebox" : "roulette";
        await _hub.Clients.Group(LiveDrawSessionGroups.For(kind, state.PublicId))
            .SendAsync(StateChangedEvent, state, cancellationToken);
        return state;
    }
}
