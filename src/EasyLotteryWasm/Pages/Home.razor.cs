using System.Text;
using System.Text.Json;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Pages;
using EasyLotteryWasm.Models;
using MiniExcelLibs;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Pages;

public partial class Home
{
    private sealed class ParticipantImportRow
    {
        public string? Name { get; set; }

        public string? Level { get; set; }

        public string? Group { get; set; }

        public string? Tags { get; set; }

        public bool IsWinner { get; set; }
    }

    private sealed class PrizeImportRow
    {
        public string? Prize { get; set; }
    }

    private string participantDataFormat = "json";
    private string participantDataText = "";
    private string prizeDataFormat = "json";
    private string prizeDataText = "";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private async Task LoadDrawingRuleStateAsync()
    {
        var document = await ConfigStore.LoadAsync();
        levelRate = new Dictionary<string, int>(document.DrawingRules.LevelRates, StringComparer.OrdinalIgnoreCase);
    }

    private async Task ExportParticipantsAsync()
    {
        var content = participantDataFormat == "csv"
            ? BuildParticipantsCsv(Participants)
            : JsonSerializer.Serialize(Participants, JsonOptions);

        await DownloadTextAsync(
            $"participants-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{GetFormatExtension(participantDataFormat)}",
            content,
            participantDataFormat == "csv" ? "text/csv;charset=utf-8" : "application/json;charset=utf-8");
    }

    private async Task ExportPrizesAsync()
    {
        var content = prizeDataFormat == "csv"
            ? BuildPrizesCsv(Prizes)
            : JsonSerializer.Serialize(Prizes, JsonOptions);

        await DownloadTextAsync(
            $"prizes-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{GetFormatExtension(prizeDataFormat)}",
            content,
            prizeDataFormat == "csv" ? "text/csv;charset=utf-8" : "application/json;charset=utf-8");
    }

    private async Task ImportParticipantsAsync()
    {
        try
        {
            var importedParticipants = ParseParticipants(participantDataText, participantDataFormat);
            if (!importedParticipants.Any())
            {
                await MessageService.Warning("沒有可匯入的參加者資料。");
                return;
            }

            Participants = importedParticipants;
            Winners.Clear();
            UpdateParticipantGroupFilter();
            EnsureLevelRatesForLevels(importedParticipants.Select(participant => participant.Level));
            await PersistLevelRatesAsync();
            await MessageService.Success($"已匯入 {Participants.Count} 筆參加者資料。");
        }
        catch (Exception ex)
        {
            await MessageService.Error($"參加者匯入失敗：{ex.Message}");
        }
    }

    private async Task ImportPrizesAsync()
    {
        try
        {
            var importedPrizes = ParsePrizes(prizeDataText, prizeDataFormat);
            if (!importedPrizes.Any())
            {
                await MessageService.Warning("沒有可匯入的獎項資料。");
                return;
            }

            Prizes = importedPrizes;
            Winners.Clear();
            await MessageService.Success($"已匯入 {Prizes.Count} 筆獎項資料。");
        }
        catch (Exception ex)
        {
            await MessageService.Error($"獎項匯入失敗：{ex.Message}");
        }
    }

    private bool EnsureLevelRatesForLevels(IEnumerable<string> levels)
    {
        var changed = false;
        foreach (var level in levels
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Select(item => item.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!levelRate.ContainsKey(level))
            {
                levelRate[level] = 1;
                changed = true;
            }
        }

        return changed;
    }

    private async Task PersistLevelRatesAsync()
    {
        var document = await ConfigStore.LoadAsync();
        document.DrawingRules.LevelRates = new Dictionary<string, int>(levelRate, StringComparer.OrdinalIgnoreCase);
        await ConfigStore.SaveAsync(document);
    }

    private async Task LoadDrawPrizeAsync()
    {
        if (YTMembers.Any())
        {
            EnsureLevelRatesForLevels(YTMembers.Select(member => member.Level));

            Participants = YTMembers.SelectMany(x => Enumerable.Repeat(
                new Participant { Name = x.Name, Level = x.Level, Group = "YT會員", Tags = new List<string>(), IsWinner = false },
                levelRate.TryGetValue(x.Level, out var rate) ? rate : 1)).ToList();
            UpdateParticipantGroupFilter();
        }

        await PersistLevelRatesAsync();
    }

    private static List<Participant> ParseParticipants(string content, string format)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<Participant>();
        }

        return format == "csv"
            ? ParseParticipantsCsv(content)
            : ParseParticipantsJson(content);
    }

    private static List<string> ParsePrizes(string content, string format)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<string>();
        }

        return format == "csv"
            ? ParsePrizesCsv(content)
            : ParsePrizesJson(content);
    }

    private static List<Participant> ParseParticipantsJson(string content)
    {
        var participants = JsonSerializer.Deserialize<List<Participant>>(content) ?? new List<Participant>();
        return participants.Select(NormalizeParticipant).ToList();
    }

    private static List<string> ParsePrizesJson(string content)
    {
        return JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();
    }

    private static List<Participant> ParseParticipantsCsv(string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var rows = stream.Query<ParticipantImportRow>(excelType: ExcelType.CSV).ToList();

        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new Participant
            {
                Name = row.Name?.Trim() ?? "",
                Level = row.Level?.Trim() ?? "",
                Group = row.Group?.Trim() ?? "",
                Tags = ParseTags(row.Tags),
                IsWinner = row.IsWinner
            })
            .Select(NormalizeParticipant)
            .ToList();
    }

    private static List<string> ParsePrizesCsv(string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var rows = stream.Query<PrizeImportRow>(excelType: ExcelType.CSV).ToList();

        return rows
            .Select(row => row.Prize?.Trim() ?? "")
            .Where(prize => !string.IsNullOrWhiteSpace(prize))
            .ToList();
    }

    private static string BuildParticipantsCsv(IEnumerable<Participant> participants)
    {
        var lines = new List<string> { "Name,Level,Group,Tags,IsWinner" };
        foreach (var participant in participants)
        {
            var tags = string.Join("|", participant.Tags ?? []);
            lines.Add(string.Join(",",
                EscapeCsv(participant.Name),
                EscapeCsv(participant.Level),
                EscapeCsv(participant.Group),
                EscapeCsv(tags),
                participant.IsWinner ? "true" : "false"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildPrizesCsv(IEnumerable<string> prizes)
    {
        var lines = new List<string> { "Prize" };
        foreach (var prize in prizes)
        {
            lines.Add(EscapeCsv(prize));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task DownloadTextAsync(string fileName, string content, string mimeType)
    {
        await JSRuntime.InvokeVoidAsync("easyLotteryDownload.downloadText", fileName, content, mimeType);
    }

    private static string GetFormatExtension(string format) => format == "csv" ? "csv" : "json";

    private static string EscapeCsv(string? value)
    {
        var safeValue = value ?? string.Empty;
        if (safeValue.IndexOf('"') >= 0 ||
            safeValue.IndexOf(',') >= 0 ||
            safeValue.IndexOf('\n') >= 0 ||
            safeValue.IndexOf('\r') >= 0)
        {
            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
        }

        return safeValue;
    }
}
