using EasyLotteryDomain.Models.Config;

namespace EasyLotteryApplication.Settings;

public interface ISettingsYamlDocumentRepository
{
    Task<SettingsYamlDocument> ReadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SettingsYamlDocument document, CancellationToken cancellationToken = default);
}

public interface IActivitiesYamlDocumentRepository
{
    Task<ActivitiesYamlDocument> ReadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ActivitiesYamlDocument document, CancellationToken cancellationToken = default);
}

public interface IActivityResultsYamlDocumentRepository
{
    Task<ActivityResultsYamlDocument> ReadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ActivityResultsYamlDocument document, CancellationToken cancellationToken = default);
}
