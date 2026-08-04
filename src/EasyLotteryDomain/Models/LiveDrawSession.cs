namespace EasyLotteryDomain.Models;

public enum LiveDrawSessionKind
{
    PokeBox,
    Roulette
}

public enum LiveDrawSessionPhase
{
    Ready,
    CountingDown,
    Animating,
    ShowingResult
}

public sealed record LiveDrawSessionState
{
    public required LiveDrawSessionKind Kind { get; init; }
    public required Guid PublicId { get; init; }
    public required int TemplateId { get; init; }
    public long Revision { get; init; }
    public LiveDrawSessionPhase Phase { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset? PhaseEndsAtUtc { get; init; }
    public int? PokeCellIndex { get; init; }
    public string? ResultTitle { get; init; }
    public int? RouletteSegmentIndex { get; init; }
    public double RouletteRotationDeg { get; init; }
}

public sealed record PokeLiveDrawCommand(int? CellIndex = null, int CountdownSeconds = 2);

public sealed record RouletteLiveDrawCommand(int? ForceIndex = null, int CountdownSeconds = 3);
