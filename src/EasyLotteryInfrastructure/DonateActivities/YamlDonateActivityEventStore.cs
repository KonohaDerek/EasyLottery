using System.Text.Json;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyLotteryInfrastructure.DonateActivities;

public sealed class YamlDonateActivityEventStore : IDonateActivityEventStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IStorageGateProvider _storageGates;
    private readonly ILogger<YamlDonateActivityEventStore> _logger;
    public string EventLogPath { get; }

    public YamlDonateActivityEventStore(IConfiguration configuration, IHostEnvironment environment, IStorageGateProvider storageGates, ILogger<YamlDonateActivityEventStore> logger)
    {
        _storageGates = storageGates;
        _logger = logger;
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
        var invalidLines = new List<string>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var record = JsonSerializer.Deserialize<DonateActivityEventRecord>(line, SerializerOptions);
                if (record is not null && record.EventVersion <= 1)
                {
                    record.EventVersion = 1;
                    records.Add(record);
                }
                else
                    invalidLines.Add(line);
            }
            catch (JsonException exception)
            {
                invalidLines.Add(line);
                _logger.LogWarning(exception, "忽略 Donate 活動事件檔中的損壞資料列。檔案：{EventLogPath}", EventLogPath);
            }
        }

        if (invalidLines.Count > 0)
        {
            var quarantinePath = $"{EventLogPath}.bad.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}";
            await File.WriteAllLinesAsync(quarantinePath, invalidLines, cancellationToken);
            var temporaryPath = $"{EventLogPath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllLinesAsync(temporaryPath, records.Select(record => JsonSerializer.Serialize(record, SerializerOptions)), cancellationToken);
            File.Move(temporaryPath, EventLogPath, overwrite: true);
            _logger.LogWarning("已將 {Count} 筆無法解析的 Donate 活動事件隔離至 {QuarantinePath}。", invalidLines.Count, quarantinePath);
        }

        return records;
    }
}
