using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApplication.Settings;

public sealed record ConfigBrowserSnapshot(string Yaml, string ETag);

public sealed record ConfigBackupInfo(string Id, DateTimeOffset CreatedAtUtc, long Length);

public interface IEasyLotteryConfigRepository
{
    Task<string> ReadForBrowserAsync(CancellationToken cancellationToken);

    Task<ConfigBrowserSnapshot> ReadForBrowserSnapshotAsync(CancellationToken cancellationToken);

    Task SaveBrowserUpdateAsync(string submittedYaml, CancellationToken cancellationToken);

    Task SaveBrowserUpdateAsync(string submittedYaml, string? expectedETag, CancellationToken cancellationToken);

    IReadOnlyList<ConfigBackupInfo> ListBackups();

    Task RestoreBackupAsync(string id, CancellationToken cancellationToken);

    Task<EasyLotteryConfigDocument> ReadAsync(CancellationToken cancellationToken);

    Task<T> UpdateAsync<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken);
}
