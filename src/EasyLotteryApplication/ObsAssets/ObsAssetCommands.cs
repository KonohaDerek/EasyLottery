using EasyLotteryDomain.Models.Obs;
using MediatR;

namespace EasyLotteryApplication.ObsAssets;

public sealed record ListObsAssetsQuery : IRequest<IReadOnlyList<ObsAsset>>;
public sealed record GetObsAssetQuery(Guid Id) : IRequest<ObsAsset?>;
public sealed record UploadObsAssetCommand(
    string FileName,
    string ContentType,
    ObsAssetKind Kind,
    Stream Content,
    long Length,
    IReadOnlyCollection<string>? ReferencedBy = null) : IRequest<ObsAsset>;
public sealed record DeleteObsAssetCommand(Guid Id) : IRequest<bool>;

public sealed class ListObsAssetsQueryHandler(IObsAssetRepository repository)
    : IRequestHandler<ListObsAssetsQuery, IReadOnlyList<ObsAsset>>
{
    public Task<IReadOnlyList<ObsAsset>> Handle(ListObsAssetsQuery request, CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);
}

public sealed class GetObsAssetQueryHandler(IObsAssetRepository repository)
    : IRequestHandler<GetObsAssetQuery, ObsAsset?>
{
    public Task<ObsAsset?> Handle(GetObsAssetQuery request, CancellationToken cancellationToken) =>
        repository.GetAsync(request.Id, cancellationToken);
}

public sealed class UploadObsAssetCommandHandler(IObsAssetRepository repository)
    : IRequestHandler<UploadObsAssetCommand, ObsAsset>
{
    public Task<ObsAsset> Handle(UploadObsAssetCommand request, CancellationToken cancellationToken)
    {
        if (request.Length <= 0) throw new InvalidOperationException("資產檔案不可為空。");
        if (string.IsNullOrWhiteSpace(request.FileName)) throw new InvalidOperationException("資產檔名不可為空。");
        return repository.SaveAsync(new ObsAsset
        {
            FileName = request.FileName,
            ContentType = request.ContentType,
            Kind = request.Kind,
            Length = request.Length,
            ReferencedBy = request.ReferencedBy?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? []
        }, request.Content, cancellationToken);
    }
}

public sealed class DeleteObsAssetCommandHandler(IObsAssetRepository repository)
    : IRequestHandler<DeleteObsAssetCommand, bool>
{
    public Task<bool> Handle(DeleteObsAssetCommand request, CancellationToken cancellationToken) =>
        repository.DeleteAsync(request.Id, cancellationToken);
}
