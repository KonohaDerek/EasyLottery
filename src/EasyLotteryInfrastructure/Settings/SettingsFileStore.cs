using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryApplication.Settings;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.Settings;

/// <summary>YAML-backed repository for the EasyLottery configuration.</summary>
public sealed class SettingsFileStore : IEasyLotteryConfigRepository, IEasyLotteryConfigStore
{
    private readonly ConfigSecretRedactor _secrets;
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly YamlSettingsDocumentRepository _settingsRepository;
    private readonly YamlActivitiesDocumentRepository _activitiesRepository;
    private readonly YamlActivityResultsDocumentRepository _resultsRepository;

    public string ConfigPath { get; }

    public string ActivitiesPath { get; }

    public string ActivityResultsPath { get; }

    public SettingsFileStore(
        ConfigSecretRedactor secrets,
        YamlSettingsDocumentRepository settingsRepository,
        YamlActivitiesDocumentRepository activitiesRepository,
        YamlActivityResultsDocumentRepository resultsRepository)
    {
        _secrets = secrets;
        _settingsRepository = settingsRepository;
        _activitiesRepository = activitiesRepository;
        _resultsRepository = resultsRepository;
        ConfigPath = settingsRepository.StoragePath;
        ActivitiesPath = activitiesRepository.StoragePath;
        ActivityResultsPath = resultsRepository.StoragePath;
    }

    public async Task<string> ReadForBrowserAsync(CancellationToken cancellationToken)
    {
        var document = await ReadMergedDocumentAsync(cancellationToken);
        return _secrets.RedactForBrowser(_serializer.Serialize(document));
    }

    public async Task SaveBrowserUpdateAsync(string submittedYaml, CancellationToken cancellationToken)
    {
        var currentYaml = _serializer.Serialize(await ReadMergedDocumentAsync(cancellationToken));
        var mergedYaml = _secrets.MergeBrowserUpdate(currentYaml, submittedYaml);
        var document = string.IsNullOrWhiteSpace(mergedYaml)
            ? new EasyLotteryConfigDocument()
            : _deserializer.Deserialize<EasyLotteryConfigDocument>(mergedYaml) ?? new EasyLotteryConfigDocument();
        await SaveSplitDocumentsAsync(document, cancellationToken);
    }

    public Task<EasyLotteryConfigDocument> LoadAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(cancellationToken);

    public Task<EasyLotteryConfigDocument> ReadAsync(CancellationToken cancellationToken)
    {
        return ReadMergedDocumentAsync(cancellationToken);
    }

    public Task<T> UpdateAsync<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken)
    {
        return UpdateAsyncInternal(update, cancellationToken);
    }

    public async Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default)
    {
        await SaveSplitDocumentsAsync(document, cancellationToken);
    }

    private async Task<T> UpdateAsyncInternal<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken)
    {
        var document = await ReadMergedDocumentAsync(cancellationToken);
        var result = update(document);
        await SaveSplitDocumentsAsync(document, cancellationToken);
        return result;
    }

    private async Task<EasyLotteryConfigDocument> ReadMergedDocumentAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.ReadAsync(cancellationToken);
        var activities = await _activitiesRepository.ReadAsync(cancellationToken);
        var results = await _resultsRepository.ReadAsync(cancellationToken);
        return YamlRepositoryMapper.Merge(settings, activities, results);
    }

    private async Task SaveSplitDocumentsAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken)
    {
        var parts = YamlRepositoryMapper.Split(document);
        await _settingsRepository.SaveAsync(parts.Settings, cancellationToken);
        await _activitiesRepository.SaveAsync(parts.Activities, cancellationToken);
        await _resultsRepository.SaveAsync(parts.Results, cancellationToken);
    }
}
