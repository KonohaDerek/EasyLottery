using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using YamlDotNet.Serialization;

namespace EasyLotteryApi;

/// <summary>YAML-backed repository for the EasyLottery configuration.</summary>
public sealed class SettingsFileStore : IEasyLotteryConfigRepository
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConfigSecretRedactor _secrets;
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();

    public string ConfigPath { get; }

    public string ActivitiesPath { get; }

    public string ActivityResultsPath { get; }

    public SettingsFileStore(IConfiguration configuration, IWebHostEnvironment environment, ConfigSecretRedactor secrets)
    {
        _secrets = secrets;
        var directory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);

        ConfigPath = Path.Combine(directory, "settings.yaml");
        ActivitiesPath = Path.Combine(directory, "activities.yaml");
        ActivityResultsPath = Path.Combine(directory, "activity-results.yaml");

        EnsureRepositoryDocuments();
    }

    public async Task<string> ReadForBrowserAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadMergedDocumentUnsafeAsync(cancellationToken);
            return _secrets.RedactForBrowser(_serializer.Serialize(document));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveBrowserUpdateAsync(string submittedYaml, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var currentYaml = _serializer.Serialize(await ReadMergedDocumentUnsafeAsync(cancellationToken));
            var mergedYaml = _secrets.MergeBrowserUpdate(currentYaml, submittedYaml);
            var document = DeserializeConfigDocument(mergedYaml);
            await WriteSplitDocumentsAsync(document, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<EasyLotteryConfigDocument> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadMergedDocumentUnsafeAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<T> UpdateAsync<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadMergedDocumentUnsafeAsync(cancellationToken);
            var result = update(document);
            await WriteSplitDocumentsAsync(document, cancellationToken);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureRepositoryDocuments()
    {
        if (File.Exists(ConfigPath) && File.Exists(ActivitiesPath) && File.Exists(ActivityResultsPath))
        {
            return;
        }

        if (File.Exists(ConfigPath))
        {
            var legacyDocument = ReadLegacyDocument(ConfigPath);
            WriteSplitDocuments(legacyDocument);
            return;
        }

        var activities = ReadActivitiesDocument(ActivitiesPath);
        var results = ReadActivityResultsDocument(ActivityResultsPath);
        WriteSplitDocuments(new EasyLotteryConfigDocument
        {
            PokeTemplates = activities.PokeTemplates,
            RouletteTemplates = activities.RouletteTemplates,
            DonateLotteryActivities = activities.DonateLotteryActivities,
            ActivityResults = results.ActivityResults,
            DonateLotteryDrawRecords = results.DonateLotteryDrawRecords,
            ProcessedDonatePaymentIds = results.ProcessedDonatePaymentIds
        });
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

    private void WriteSplitDocuments(EasyLotteryConfigDocument document)
    {
        var parts = YamlRepositoryMapper.Split(document);
        WriteDocument(ConfigPath, parts.Settings);
        WriteDocument(ActivitiesPath, parts.Activities);
        WriteDocument(ActivityResultsPath, parts.Results);
    }

    private EasyLotteryConfigDocument ReadLegacyDocument(string path)
    {
        var yaml = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        yaml = _secrets.NormalizeForPersistence(yaml);
        return DeserializeConfigDocument(yaml);
    }

    private async Task<SettingsYamlDocument> ReadSettingsDocumentAsync(CancellationToken cancellationToken) =>
        await ReadDocumentAsync(ConfigPath, new SettingsYamlDocument(), cancellationToken);

    private async Task<ActivitiesYamlDocument> ReadActivitiesDocumentAsync(CancellationToken cancellationToken) =>
        await ReadDocumentAsync(ActivitiesPath, new ActivitiesYamlDocument(), cancellationToken);

    private async Task<ActivityResultsYamlDocument> ReadActivityResultsDocumentAsync(CancellationToken cancellationToken) =>
        await ReadDocumentAsync(ActivityResultsPath, new ActivityResultsYamlDocument(), cancellationToken);

    private ActivitiesYamlDocument ReadActivitiesDocument(string path) =>
        File.Exists(path) ? DeserializeDocument<ActivitiesYamlDocument>(File.ReadAllText(path)) : new ActivitiesYamlDocument();

    private ActivityResultsYamlDocument ReadActivityResultsDocument(string path) =>
        File.Exists(path) ? DeserializeDocument<ActivityResultsYamlDocument>(File.ReadAllText(path)) : new ActivityResultsYamlDocument();

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
