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
    private List<DrawingRulePreset> drawingRulePresets = new();
    private string selectedRulePresetName = "";
    private string rulePresetName = "";
    private string rulePresetDescription = "";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private void RebuildLevelRatesFromParticipants()
    {
        levelRate.Clear();

        foreach (var level in Participants
                     .Where(participant => !string.IsNullOrWhiteSpace(participant.Level))
                     .Select(participant => participant.Level)
                     .Distinct())
        {
            levelRate[level] = 1;
        }
    }

    private async Task LoadRulePresetsAsync()
    {
        var document = await ConfigStore.LoadAsync();
        drawingRulePresets = document.DrawingRulePresets
            .OrderBy(preset => preset.Name)
            .ToList();

        if (string.IsNullOrWhiteSpace(selectedRulePresetName) && drawingRulePresets.Any())
        {
            selectedRulePresetName = drawingRulePresets[0].Name;
        }
    }

    private async Task SaveCurrentRulePresetAsync()
    {
        if (string.IsNullOrWhiteSpace(rulePresetName))
        {
            await MessageService.Warning("請輸入規則模板名稱。");
            return;
        }

        var document = await ConfigStore.LoadAsync();
        var preset = new DrawingRulePreset
        {
            Name = rulePresetName.Trim(),
            Description = rulePresetDescription.Trim(),
            LevelRates = new Dictionary<string, int>(levelRate, StringComparer.OrdinalIgnoreCase)
        };

        var existingIndex = document.DrawingRulePresets.FindIndex(item => string.Equals(item.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            document.DrawingRulePresets[existingIndex] = preset;
        }
        else
        {
            document.DrawingRulePresets.Add(preset);
        }

        await ConfigStore.SaveAsync(document);
        await LoadRulePresetsAsync();
        selectedRulePresetName = preset.Name;
        await MessageService.Success($"已儲存規則模板「{preset.Name}」。");
    }

    private async Task ApplySelectedRulePresetAsync()
    {
        if (string.IsNullOrWhiteSpace(selectedRulePresetName))
        {
            await MessageService.Warning("請先選擇規則模板。");
            return;
        }

        var document = await ConfigStore.LoadAsync();
        var preset = document.DrawingRulePresets
            .FirstOrDefault(item => string.Equals(item.Name, selectedRulePresetName, StringComparison.OrdinalIgnoreCase));

        if (preset == null)
        {
            await MessageService.Warning("找不到指定的規則模板。");
            return;
        }

        levelRate = new Dictionary<string, int>(preset.LevelRates, StringComparer.OrdinalIgnoreCase);
        LoadDrawPrize();
        await MessageService.Success($"已套用規則模板「{preset.Name}」。");
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
            RebuildLevelRatesFromParticipants();
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
        return JsonSerializer.Deserialize<List<Participant>>(content) ?? new List<Participant>();
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
                IsWinner = row.IsWinner
            })
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
        var lines = new List<string> { "Name,Level,IsWinner" };
        foreach (var participant in participants)
        {
            lines.Add(string.Join(",",
                EscapeCsv(participant.Name),
                EscapeCsv(participant.Level),
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
