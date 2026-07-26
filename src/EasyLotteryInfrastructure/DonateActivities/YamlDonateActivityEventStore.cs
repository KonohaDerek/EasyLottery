using System.Text.Json;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryInfrastructure.DonateActivities;

public sealed class YamlDonateActivityEventStore : IDonateActivityEventStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IStorageGateProvider _storageGates;
    public string EventLogPath { get; }

    public YamlDonateActivityEventStore(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates)
    {
        _storageGates = storageGates;
        var directory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);
        EventLogPath = Path.Combine(directory, "donate-activity-events.jsonl");
    }

    public async Task AppendAsync(DonateActivityEventRecord record, CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, EventLogPath);
        await AppendUnsafeAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<DonateActivityEventRecord>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _storageGates.AcquireAsync(cancellationToken, EventLogPath);
        return await ReadAllUnsafeAsync(cancellationToken);
    }

    internal async Task AppendUnsafeAsync(DonateActivityEventRecord record, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(EventLogPath) ?? ".");
        await File.AppendAllTextAsync(EventLogPath, JsonSerializer.Serialize(record, SerializerOptions) + Environment.NewLine, cancellationToken);
    }

    internal async Task<IReadOnlyList<DonateActivityEventRecord>> ReadAllUnsafeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(EventLogPath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(EventLogPath, cancellationToken);
        var records = new List<DonateActivityEventRecord>(lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<DonateActivityEventRecord>(line, SerializerOptions);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }
}
