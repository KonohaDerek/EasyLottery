using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryApplication.Templates;

public interface IPokeTemplateRepository
{
    Task<IReadOnlyList<PokeTemplate>> ListAsync(CancellationToken cancellationToken = default);
    Task<PokeTemplate?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<PokeTemplate?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);
    Task<PokeTemplate> CreateAsync(PokeTemplate template, CancellationToken cancellationToken = default);
    Task UpdateAsync(PokeTemplate template, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<PokeTemplate> DuplicateAsync(int id, CancellationToken cancellationToken = default);
    Task<PokeTemplate> SetPublicationStatusAsync(int id, TemplatePublicationStatus status, CancellationToken cancellationToken = default);
    Task<PokeCell?> PokeRandomAsync(int id, CancellationToken cancellationToken = default);
    Task<PokeCell?> PokeCellAsync(int id, int index, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PokeCell>> GetRevealStateAsync(int id, CancellationToken cancellationToken = default);
    Task ResetAsync(int id, CancellationToken cancellationToken = default);
    Task<string> ExportAsync(int id, CancellationToken cancellationToken = default);
    Task<PokeTemplate> ImportAsync(string json, CancellationToken cancellationToken = default);
    Task SeedDefaultsAsync(CancellationToken cancellationToken = default);
}

public interface IRouletteTemplateRepository
{
    Task<IReadOnlyList<RouletteTemplate>> ListAsync(CancellationToken cancellationToken = default);
    Task<RouletteTemplate?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<RouletteTemplate?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);
    Task<RouletteTemplate> CreateAsync(RouletteTemplate template, CancellationToken cancellationToken = default);
    Task UpdateAsync(RouletteTemplate template, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<RouletteTemplate> DuplicateAsync(int id, CancellationToken cancellationToken = default);
    Task<RouletteTemplate> SetPublicationStatusAsync(int id, TemplatePublicationStatus status, CancellationToken cancellationToken = default);
    Task<SpinResult> SpinAsync(int id, int? forceIndex = null, double currentRotation = 0, CancellationToken cancellationToken = default);
    Task<string> ExportAsync(int id, CancellationToken cancellationToken = default);
    Task<RouletteTemplate> ImportAsync(string json, CancellationToken cancellationToken = default);
    Task SeedDefaultsAsync(CancellationToken cancellationToken = default);
}

public interface IActivityResultRepository
{
    Task<IReadOnlyList<ActivityResultRecord>> ListAsync(CancellationToken cancellationToken = default);
    Task<ActivityResultRecord?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<ActivityResultRecord> RecordPokeAsync(int templateId, CancellationToken cancellationToken = default);
    Task<ActivityResultRecord> RecordRouletteAsync(int templateId, SpinResult spinResult, CancellationToken cancellationToken = default);
}
