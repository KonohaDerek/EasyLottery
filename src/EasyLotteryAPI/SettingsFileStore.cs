using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;

namespace EasyLotteryApi;

/// <summary>YAML-backed repository for the EasyLottery configuration.</summary>
public sealed class SettingsFileStore : IEasyLotteryConfigRepository, IEasyLotteryConfigStore
{
    private readonly IStorageGateProvider _storageGates;
    private readonly ConfigSecretRedactor _secrets;
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();

    public string ConfigPath { get; }

    public string ActivitiesPath { get; }

    public string ActivityResultsPath { get; }

    public SettingsFileStore(IConfiguration configuration, IHostEnvironment environment, ConfigSecretRedactor secrets, IStorageGateProvider storageGates)
    {
        _secrets = secrets;
        _storageGates = storageGates;
        var directory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);

        ConfigPath = Path.Combine(directory, "settings.yaml");
        ActivitiesPath = Path.Combine(directory, "activities.yaml");
        ActivityResultsPath = Path.Combine(directory, "activity-results.yaml");

        EnsureRepositoryDocuments();
    }

    public async Task<string> ReadForBrowserAsync(CancellationToken cancellationToken)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, ConfigPath, ActivitiesPath, ActivityResultsPath);
        var document = await ReadMergedDocumentUnsafeAsync(cancellationToken);
        return _secrets.RedactForBrowser(_serializer.Serialize(document));
    }

    public async Task SaveBrowserUpdateAsync(string submittedYaml, CancellationToken cancellationToken)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, ConfigPath, ActivitiesPath, ActivityResultsPath);
        var currentYaml = _serializer.Serialize(await ReadMergedDocumentUnsafeAsync(cancellationToken));
        var mergedYaml = _secrets.MergeBrowserUpdate(currentYaml, submittedYaml);
        var document = DeserializeConfigDocument(mergedYaml);
        await WriteSplitDocumentsAsync(document, cancellationToken);
    }

    public async Task<EasyLotteryConfigDocument> ReadAsync(CancellationToken cancellationToken)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, ConfigPath, ActivitiesPath, ActivityResultsPath);
        return await ReadMergedDocumentUnsafeAsync(cancellationToken);
    }

    public async Task<T> UpdateAsync<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, ConfigPath, ActivitiesPath, ActivityResultsPath);
        var document = await ReadMergedDocumentUnsafeAsync(cancellationToken);
        var result = update(document);
        await WriteSplitDocumentsAsync(document, cancellationToken);
        return result;
    }

    public Task<EasyLotteryConfigDocument> LoadAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(cancellationToken);

    public async Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, ConfigPath, ActivitiesPath, ActivityResultsPath);
        await WriteSplitDocumentsAsync(document, cancellationToken);
    }

    private void EnsureRepositoryDocuments()
    {
        if (!File.Exists(ConfigPath))
        {
            WriteDocument(ConfigPath, new SettingsYamlDocument());
        }

        if (!File.Exists(ActivitiesPath))
        {
            WriteDocument(ActivitiesPath, new ActivitiesYamlDocument());
        }

        if (!File.Exists(ActivityResultsPath))
        {
            WriteDocument(ActivityResultsPath, new ActivityResultsYamlDocument());
        }
    }

    private async Task<EasyLotteryConfigDocument> ReadMergedDocumentUnsafeAsync(CancellationToken cancellationToken)
    {
        var settings = await ReadSettingsDocumentAsync(cancellationToken);
        var activities = await ReadActivitiesDocumentAsync(cancellationToken);
        var results = await ReadActivityResultsDocumentAsync(cancellationToken);
        return YamlRepositoryMapper.Merge(settings, activities, results);
    }

    private async Task WriteSplitDocumentsAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken)
    {
        var parts = YamlRepositoryMapper.Split(document);
        await WriteDocumentAsync(ConfigPath, parts.Settings, cancellationToken);
        await WriteDocumentAsync(ActivitiesPath, parts.Activities, cancellationToken);
        await WriteDocumentAsync(ActivityResultsPath, parts.Results, cancellationToken);
    }

    private async Task<SettingsYamlDocument> ReadSettingsDocumentAsync(CancellationToken cancellationToken) =>
        await ReadDocumentAsync(ConfigPath, new SettingsYamlDocument(), cancellationToken);

    private async Task<ActivitiesYamlDocument> ReadActivitiesDocumentAsync(CancellationToken cancellationToken) =>
        await ReadDocumentAsync(ActivitiesPath, new ActivitiesYamlDocument(), cancellationToken);

    private async Task<ActivityResultsYamlDocument> ReadActivityResultsDocumentAsync(CancellationToken cancellationToken) =>
        await ReadDocumentAsync(ActivityResultsPath, new ActivityResultsYamlDocument(), cancellationToken);

    private async Task<T> ReadDocumentAsync<T>(string path, T fallback, CancellationToken cancellationToken) where T : class
    {
        if (!File.Exists(path))
        {
            return fallback;
        }

        var yaml = await File.ReadAllTextAsync(path, cancellationToken);
        return string.IsNullOrWhiteSpace(yaml) ? fallback : DeserializeDocument<T>(yaml) ?? fallback;
    }

    private EasyLotteryConfigDocument DeserializeConfigDocument(string yaml) =>
        string.IsNullOrWhiteSpace(yaml) ? new EasyLotteryConfigDocument() : DeserializeDocument<EasyLotteryConfigDocument>(yaml) ?? new EasyLotteryConfigDocument();

    private T DeserializeDocument<T>(string yaml) where T : class =>
        _deserializer.Deserialize<T>(yaml) ?? throw new InvalidOperationException($"Unable to deserialize YAML document to {typeof(T).Name}.");

    private async Task WriteDocumentAsync<T>(string path, T document, CancellationToken cancellationToken) where T : class
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, _serializer.Serialize(document), cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private void WriteDocument<T>(string path, T document) where T : class
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, _serializer.Serialize(document));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
