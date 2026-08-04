using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Entities;
using MediatR;

namespace EasyLotteryApplication.Templates;

public sealed record GetPokeTemplatesQuery : IRequest<IReadOnlyList<PokeTemplate>>;
public sealed record GetPokeTemplateQuery(int Id) : IRequest<PokeTemplate?>;
public sealed record GetPokeTemplateByPublicIdQuery(Guid PublicId) : IRequest<PokeTemplate?>;
public sealed record SavePokeTemplateCommand(PokeTemplate Template) : IRequest<PokeTemplate>;
public sealed record DeletePokeTemplateCommand(int Id) : IRequest<Unit>;
public sealed record DuplicatePokeTemplateCommand(int Id) : IRequest<PokeTemplate>;
public sealed record SetPokePublicationStatusCommand(int Id, TemplatePublicationStatus Status) : IRequest<PokeTemplate>;
public sealed record PokeCellCommand(int Id, int? Index) : IRequest<PokeCell?>;
public sealed record GetPokeRevealStateQuery(int Id) : IRequest<IReadOnlyList<PokeCell>>;
public sealed record ResetPokeTemplateCommand(int Id) : IRequest<Unit>;
public sealed record ExportPokeTemplateQuery(int Id) : IRequest<string>;
public sealed record ImportPokeTemplateCommand(string Json) : IRequest<PokeTemplate>;
public sealed record SeedPokeTemplatesCommand : IRequest<Unit>;

public sealed record GetRouletteTemplatesQuery : IRequest<IReadOnlyList<RouletteTemplate>>;
public sealed record GetRouletteTemplateQuery(int Id) : IRequest<RouletteTemplate?>;
public sealed record GetRouletteTemplateByPublicIdQuery(Guid PublicId) : IRequest<RouletteTemplate?>;
public sealed record SaveRouletteTemplateCommand(RouletteTemplate Template) : IRequest<RouletteTemplate>;
public sealed record DeleteRouletteTemplateCommand(int Id) : IRequest<Unit>;
public sealed record DuplicateRouletteTemplateCommand(int Id) : IRequest<RouletteTemplate>;
public sealed record SetRoulettePublicationStatusCommand(int Id, TemplatePublicationStatus Status) : IRequest<RouletteTemplate>;
public sealed record SpinRouletteCommand(int Id, int? ForceIndex, double CurrentRotation) : IRequest<SpinResult>;
public sealed record ExportRouletteTemplateQuery(int Id) : IRequest<string>;
public sealed record ImportRouletteTemplateCommand(string Json) : IRequest<RouletteTemplate>;
public sealed record SeedRouletteTemplatesCommand : IRequest<Unit>;

public sealed record GetActivityResultsQuery : IRequest<IReadOnlyList<ActivityResultRecord>>;
public sealed record GetActivityResultQuery(int Id) : IRequest<ActivityResultRecord?>;
public sealed record RecordPokeResultCommand(int TemplateId) : IRequest<ActivityResultRecord>;
public sealed record RecordRouletteResultCommand(int TemplateId, SpinResult SpinResult) : IRequest<ActivityResultRecord>;

public sealed class TemplateCommandHandlers :
    IRequestHandler<GetPokeTemplatesQuery, IReadOnlyList<PokeTemplate>>,
    IRequestHandler<GetPokeTemplateQuery, PokeTemplate?>,
    IRequestHandler<GetPokeTemplateByPublicIdQuery, PokeTemplate?>,
    IRequestHandler<SavePokeTemplateCommand, PokeTemplate>,
    IRequestHandler<DeletePokeTemplateCommand, Unit>,
    IRequestHandler<DuplicatePokeTemplateCommand, PokeTemplate>,
    IRequestHandler<SetPokePublicationStatusCommand, PokeTemplate>,
    IRequestHandler<PokeCellCommand, PokeCell?>,
    IRequestHandler<GetPokeRevealStateQuery, IReadOnlyList<PokeCell>>,
    IRequestHandler<ResetPokeTemplateCommand, Unit>,
    IRequestHandler<ExportPokeTemplateQuery, string>,
    IRequestHandler<ImportPokeTemplateCommand, PokeTemplate>,
    IRequestHandler<SeedPokeTemplatesCommand, Unit>,
    IRequestHandler<GetRouletteTemplatesQuery, IReadOnlyList<RouletteTemplate>>,
    IRequestHandler<GetRouletteTemplateQuery, RouletteTemplate?>,
    IRequestHandler<GetRouletteTemplateByPublicIdQuery, RouletteTemplate?>,
    IRequestHandler<SaveRouletteTemplateCommand, RouletteTemplate>,
    IRequestHandler<DeleteRouletteTemplateCommand, Unit>,
    IRequestHandler<DuplicateRouletteTemplateCommand, RouletteTemplate>,
    IRequestHandler<SetRoulettePublicationStatusCommand, RouletteTemplate>,
    IRequestHandler<SpinRouletteCommand, SpinResult>,
    IRequestHandler<ExportRouletteTemplateQuery, string>,
    IRequestHandler<ImportRouletteTemplateCommand, RouletteTemplate>,
    IRequestHandler<SeedRouletteTemplatesCommand, Unit>,
    IRequestHandler<GetActivityResultsQuery, IReadOnlyList<ActivityResultRecord>>,
    IRequestHandler<GetActivityResultQuery, ActivityResultRecord?>,
    IRequestHandler<RecordPokeResultCommand, ActivityResultRecord>,
    IRequestHandler<RecordRouletteResultCommand, ActivityResultRecord>
{
    private readonly IPokeTemplateRepository _pokes;
    private readonly IRouletteTemplateRepository _roulettes;
    private readonly IActivityResultRepository _results;

    public TemplateCommandHandlers(IPokeTemplateRepository pokes, IRouletteTemplateRepository roulettes, IActivityResultRepository results)
    {
        _pokes = pokes;
        _roulettes = roulettes;
        _results = results;
    }

    public Task<IReadOnlyList<PokeTemplate>> Handle(GetPokeTemplatesQuery request, CancellationToken ct) => _pokes.ListAsync(ct);
    public Task<PokeTemplate?> Handle(GetPokeTemplateQuery request, CancellationToken ct) => _pokes.GetAsync(request.Id, ct);
    public Task<PokeTemplate?> Handle(GetPokeTemplateByPublicIdQuery request, CancellationToken ct) => _pokes.GetByPublicIdAsync(request.PublicId, ct);
    public async Task<PokeTemplate> Handle(SavePokeTemplateCommand request, CancellationToken ct)
    {
        if (request.Template.Id == 0) return await _pokes.CreateAsync(request.Template, ct);
        await _pokes.UpdateAsync(request.Template, ct);
        return await _pokes.GetAsync(request.Template.Id, ct) ?? request.Template;
    }
    public async Task<Unit> Handle(DeletePokeTemplateCommand request, CancellationToken ct) { await _pokes.DeleteAsync(request.Id, ct); return Unit.Value; }
    public Task<PokeTemplate> Handle(DuplicatePokeTemplateCommand request, CancellationToken ct) => _pokes.DuplicateAsync(request.Id, ct);
    public Task<PokeTemplate> Handle(SetPokePublicationStatusCommand request, CancellationToken ct) => _pokes.SetPublicationStatusAsync(request.Id, request.Status, ct);
    public Task<PokeCell?> Handle(PokeCellCommand request, CancellationToken ct) => request.Index.HasValue ? _pokes.PokeCellAsync(request.Id, request.Index.Value, ct) : _pokes.PokeRandomAsync(request.Id, ct);
    public Task<IReadOnlyList<PokeCell>> Handle(GetPokeRevealStateQuery request, CancellationToken ct) => _pokes.GetRevealStateAsync(request.Id, ct);
    public async Task<Unit> Handle(ResetPokeTemplateCommand request, CancellationToken ct) { await _pokes.ResetAsync(request.Id, ct); return Unit.Value; }
    public Task<string> Handle(ExportPokeTemplateQuery request, CancellationToken ct) => _pokes.ExportAsync(request.Id, ct);
    public Task<PokeTemplate> Handle(ImportPokeTemplateCommand request, CancellationToken ct) => _pokes.ImportAsync(request.Json, ct);
    public async Task<Unit> Handle(SeedPokeTemplatesCommand request, CancellationToken ct) { await _pokes.SeedDefaultsAsync(ct); return Unit.Value; }

    public Task<IReadOnlyList<RouletteTemplate>> Handle(GetRouletteTemplatesQuery request, CancellationToken ct) => _roulettes.ListAsync(ct);
    public Task<RouletteTemplate?> Handle(GetRouletteTemplateQuery request, CancellationToken ct) => _roulettes.GetAsync(request.Id, ct);
    public Task<RouletteTemplate?> Handle(GetRouletteTemplateByPublicIdQuery request, CancellationToken ct) => _roulettes.GetByPublicIdAsync(request.PublicId, ct);
    public async Task<RouletteTemplate> Handle(SaveRouletteTemplateCommand request, CancellationToken ct)
    {
        if (request.Template.Id == 0) return await _roulettes.CreateAsync(request.Template, ct);
        await _roulettes.UpdateAsync(request.Template, ct);
        return await _roulettes.GetAsync(request.Template.Id, ct) ?? request.Template;
    }
    public async Task<Unit> Handle(DeleteRouletteTemplateCommand request, CancellationToken ct) { await _roulettes.DeleteAsync(request.Id, ct); return Unit.Value; }
    public Task<RouletteTemplate> Handle(DuplicateRouletteTemplateCommand request, CancellationToken ct) => _roulettes.DuplicateAsync(request.Id, ct);
    public Task<RouletteTemplate> Handle(SetRoulettePublicationStatusCommand request, CancellationToken ct) => _roulettes.SetPublicationStatusAsync(request.Id, request.Status, ct);
    public Task<SpinResult> Handle(SpinRouletteCommand request, CancellationToken ct) => _roulettes.SpinAsync(request.Id, request.ForceIndex, request.CurrentRotation, ct);
    public Task<string> Handle(ExportRouletteTemplateQuery request, CancellationToken ct) => _roulettes.ExportAsync(request.Id, ct);
    public Task<RouletteTemplate> Handle(ImportRouletteTemplateCommand request, CancellationToken ct) => _roulettes.ImportAsync(request.Json, ct);
    public async Task<Unit> Handle(SeedRouletteTemplatesCommand request, CancellationToken ct) { await _roulettes.SeedDefaultsAsync(ct); return Unit.Value; }

    public Task<IReadOnlyList<ActivityResultRecord>> Handle(GetActivityResultsQuery request, CancellationToken ct) => _results.ListAsync(ct);
    public Task<ActivityResultRecord?> Handle(GetActivityResultQuery request, CancellationToken ct) => _results.GetAsync(request.Id, ct);
    public Task<ActivityResultRecord> Handle(RecordPokeResultCommand request, CancellationToken ct) => _results.RecordPokeAsync(request.TemplateId, ct);
    public Task<ActivityResultRecord> Handle(RecordRouletteResultCommand request, CancellationToken ct) => _results.RecordRouletteAsync(request.TemplateId, request.SpinResult, ct);
}
