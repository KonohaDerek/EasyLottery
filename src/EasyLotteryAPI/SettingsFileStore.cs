using EasyLotteryDomain.Models.Config;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EasyLotteryApi;

/// <summary>Single-writer access to the shared YAML settings file.</summary>
public sealed class SettingsFileStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConfigSecretRedactor _secrets;
    private readonly IDeserializer _deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
    private readonly ISerializer _serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults).Build();

    public string ConfigPath { get; }

    public SettingsFileStore(IConfiguration configuration, IWebHostEnvironment environment, ConfigSecretRedactor secrets)
    {
        _secrets = secrets;
        var directory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);
        ConfigPath = Path.Combine(directory, "settings.yaml");
        var legacyPath = Path.Combine(directory, "easy-lottery.yaml");
        if (!File.Exists(ConfigPath) && File.Exists(legacyPath)) File.Move(legacyPath, ConfigPath);

        if (!File.Exists(ConfigPath))
        {
            // Always materialize the shared settings file on startup so operators
            // can locate and manage it even before the first browser write.
            File.WriteAllText(ConfigPath, string.Empty);
        }

        if (File.Exists(ConfigPath))
        {
            var current = File.ReadAllText(ConfigPath);
            var normalized = _secrets.NormalizeForPersistence(current);
            if (!string.Equals(current, normalized, StringComparison.Ordinal)) File.WriteAllText(ConfigPath, normalized);
        }
    }

    public async Task<string> ReadForBrowserAsync(CancellationToken cancellationToken) =>
        _secrets.RedactForBrowser(await ReadRawAsync(cancellationToken));

    public async Task SaveBrowserUpdateAsync(string submittedYaml, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await WriteRawAsync(_secrets.MergeBrowserUpdate(await ReadRawUnsafeAsync(cancellationToken), submittedYaml), cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task<EasyLotteryConfigDocument> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return Deserialize(await ReadRawUnsafeAsync(cancellationToken)); }
        finally { _gate.Release(); }
    }

    public async Task<T> UpdateAsync<T>(Func<EasyLotteryConfigDocument, T> update, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = Deserialize(await ReadRawUnsafeAsync(cancellationToken));
            var result = update(document);
            await WriteRawAsync(_serializer.Serialize(document), cancellationToken);
            return result;
        }
        finally { _gate.Release(); }
    }

    private async Task<string> ReadRawAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await ReadRawUnsafeAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    private Task<string> ReadRawUnsafeAsync(CancellationToken cancellationToken) => File.Exists(ConfigPath) ? File.ReadAllTextAsync(ConfigPath, cancellationToken) : Task.FromResult(string.Empty);
    private EasyLotteryConfigDocument Deserialize(string yaml) => string.IsNullOrWhiteSpace(yaml) ? new() : _deserializer.Deserialize<EasyLotteryConfigDocument>(yaml) ?? new();
    private async Task WriteRawAsync(string content, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{ConfigPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
        File.Move(temporaryPath, ConfigPath, overwrite: true);
    }
}
