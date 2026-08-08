using EasyLotteryDomain.Models.Obs;

namespace EasyLotteryApplication.ObsAssets;

public interface IObsAssetRepository
{
    Task<IReadOnlyList<ObsAsset>> ListAsync(CancellationToken cancellationToken = default);
    Task<ObsAsset?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ObsAsset> SaveAsync(ObsAsset asset, Stream content, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Stream> ExportPackageAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ObsAsset>> ImportPackageAsync(Stream package, CancellationToken cancellationToken = default);
}
