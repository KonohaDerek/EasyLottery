using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;

namespace EasyLotteryInfrastructure.Settings;

public abstract class YamlDocumentRepositoryBase<T> where T : class, new()
{
    private readonly IStorageGateProvider _storageGates;
    private readonly ISerializer _serializer = YamlSerialization.CreateSerializerBuilder().Build();
    private readonly IDeserializer _deserializer = YamlSerialization.CreateDeserializerBuilder().Build();

    protected YamlDocumentRepositoryBase(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates, string fileName)
    {
        _storageGates = storageGates;
        var storageDirectory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(storageDirectory);
        StoragePath = Path.Combine(storageDirectory, fileName);
        EnsureExists();
    }

    public string StoragePath { get; }

    public async Task<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        if (!File.Exists(StoragePath))
        {
            return new T();
        }

        var yaml = await File.ReadAllTextAsync(StoragePath, cancellationToken);
        return string.IsNullOrWhiteSpace(yaml) ? new T() : _deserializer.Deserialize<T>(yaml) ?? new T();
    }

    public async Task SaveAsync(T document, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, StoragePath);
        var temporaryPath = $"{StoragePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, _serializer.Serialize(document), cancellationToken);
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }

    private void EnsureExists()
    {
        if (File.Exists(StoragePath))
        {
            return;
        }

        var temporaryPath = $"{StoragePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, _serializer.Serialize(new T()));
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }
}

public sealed class YamlSettingsDocumentRepository : YamlDocumentRepositoryBase<SettingsYamlDocument>, ISettingsYamlDocumentRepository
{
    public YamlSettingsDocumentRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
        : base(configuration, environment, storageGates, "settings.yaml")
    {
    }
}

public sealed class YamlActivitiesDocumentRepository : YamlDocumentRepositoryBase<ActivitiesYamlDocument>, IActivitiesYamlDocumentRepository
{
    public YamlActivitiesDocumentRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
        : base(configuration, environment, storageGates, "activities.yaml")
    {
    }
}

public sealed class YamlActivityResultsDocumentRepository : YamlDocumentRepositoryBase<ActivityResultsYamlDocument>, IActivityResultsYamlDocumentRepository
{
    public YamlActivityResultsDocumentRepository(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
        : base(configuration, environment, storageGates, "activity-results.yaml")
    {
    }
}
