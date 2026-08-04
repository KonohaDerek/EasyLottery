using EasyLotteryApplication.Templates;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;

namespace EasyLotteryInfrastructure.Templates;

public sealed class PokeTemplateRepository(PokeService service) : IPokeTemplateRepository
{
    public async Task<IReadOnlyList<PokeTemplate>> ListAsync(CancellationToken ct = default) => await service.ListTemplatesAsync();
    public Task<PokeTemplate?> GetAsync(int id, CancellationToken ct = default) => service.LoadTemplateAsync(id);
    public Task<PokeTemplate?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) => service.LoadTemplateAsync(publicId);
    public Task<PokeTemplate> CreateAsync(PokeTemplate template, CancellationToken ct = default) => service.CreateTemplateAsync(template);
    public Task UpdateAsync(PokeTemplate template, CancellationToken ct = default) => service.UpdateTemplateAsync(template);
    public Task DeleteAsync(int id, CancellationToken ct = default) => service.DeleteTemplateAsync(id);
    public Task<PokeTemplate> DuplicateAsync(int id, CancellationToken ct = default) => service.DuplicateTemplateAsync(id);
    public Task<PokeTemplate> SetPublicationStatusAsync(int id, TemplatePublicationStatus status, CancellationToken ct = default) => service.SetPublicationStatusAsync(id, status);
    public Task<PokeCell?> PokeRandomAsync(int id, CancellationToken ct = default) => service.PokeRandomCellAsync(id);
    public Task<PokeCell?> PokeCellAsync(int id, int index, CancellationToken ct = default) => service.PokeCellByIndexAsync(id, index);
    public async Task<IReadOnlyList<PokeCell>> GetRevealStateAsync(int id, CancellationToken ct = default) => await service.GetRevealStateAsync(id);
    public Task ResetAsync(int id, CancellationToken ct = default) => service.ResetTemplateAsync(id);
    public Task<string> ExportAsync(int id, CancellationToken ct = default) => service.ExportTemplateAsync(id);
    public Task<PokeTemplate> ImportAsync(string json, CancellationToken ct = default) => service.ImportTemplateAsync(json);
    public Task SeedDefaultsAsync(CancellationToken ct = default) => service.SeedDefaultTemplatesAsync();
}

public sealed class RouletteTemplateRepository(RouletteService service) : IRouletteTemplateRepository
{
    public async Task<IReadOnlyList<RouletteTemplate>> ListAsync(CancellationToken ct = default) => await service.ListTemplatesAsync();
    public Task<RouletteTemplate?> GetAsync(int id, CancellationToken ct = default) => service.LoadTemplateAsync(id);
    public Task<RouletteTemplate?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) => service.LoadTemplateAsync(publicId);
    public Task<RouletteTemplate> CreateAsync(RouletteTemplate template, CancellationToken ct = default) => service.CreateTemplateAsync(template);
    public Task UpdateAsync(RouletteTemplate template, CancellationToken ct = default) => service.UpdateTemplateAsync(template);
    public Task DeleteAsync(int id, CancellationToken ct = default) => service.DeleteTemplateAsync(id);
    public Task<RouletteTemplate> DuplicateAsync(int id, CancellationToken ct = default) => service.DuplicateTemplateAsync(id);
    public Task<RouletteTemplate> SetPublicationStatusAsync(int id, TemplatePublicationStatus status, CancellationToken ct = default) => service.SetPublicationStatusAsync(id, status);
    public Task<SpinResult> SpinAsync(int id, int? forceIndex = null, double currentRotation = 0, CancellationToken ct = default) => service.SpinAsync(id, forceIndex, currentRotation);
    public Task<string> ExportAsync(int id, CancellationToken ct = default) => service.ExportTemplateAsync(id);
    public Task<RouletteTemplate> ImportAsync(string json, CancellationToken ct = default) => service.ImportTemplateAsync(json);
    public Task SeedDefaultsAsync(CancellationToken ct = default) => service.SeedDefaultTemplatesAsync();
}

public sealed class ActivityResultRepository(ActivityResultService service) : IActivityResultRepository
{
    public async Task<IReadOnlyList<ActivityResultRecord>> ListAsync(CancellationToken ct = default) => await service.ListActivityResultsAsync(ct);
    public Task<ActivityResultRecord?> GetAsync(int id, CancellationToken ct = default) => service.LoadActivityResultAsync(id, ct);
    public Task<ActivityResultRecord> RecordPokeAsync(int templateId, CancellationToken ct = default) => service.RecordPokeActivityAsync(templateId, ct);
    public Task<ActivityResultRecord> RecordRouletteAsync(int templateId, SpinResult spinResult, CancellationToken ct = default) => service.RecordRouletteActivityAsync(templateId, spinResult, ct);
}
