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

    private sealed class LevelRateRow
    {
        public string Level { get; set; } = "";

        public int Rate { get; set; } = 1;
    }

    private string participantDataFormat = "json";
    private string participantDataText = "";
    private string prizeDataFormat = "json";
    private string prizeDataText = "";
    private List<DrawingRulePreset> drawingRulePresets = new();
    private string selectedRulePresetName = "";
    private string rulePresetName = "";
    private string rulePresetDescription = "";
    private List<LevelRateRow> levelRateRows = new();
    private string newLevelName = "";
    private int newLevelRate = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

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

    private async Task LoadDrawingRuleStateAsync()
    {
        var document = await ConfigStore.LoadAsync();
        SetLevelRates(document.DrawingRules.LevelRates);
    }

    private async Task SaveCurrentRulePresetAsync()
    {
        if (string.IsNullOrWhiteSpace(rulePresetName))
        {
            await MessageService.Warning("請輸入規則模板名稱。");
            return;
        }

        if (!TrySyncLevelRatesFromRows(out var errorMessage))
        {
            await MessageService.Warning(errorMessage ?? "倍率設定有重複或無效資料。");
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
        await AuditService.RecordAsync("Template", "儲存規則模板", preset.Name, $"模板包含 {preset.LevelRates.Count} 個等級", changedBy: document.SystemSettings.Audit.ActorName);
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

        SetLevelRates(preset.LevelRates);
        await PersistLevelRatesAsync();
        await AuditService.RecordAsync("Template", "套用規則模板", preset.Name, $"套用 {preset.LevelRates.Count} 個等級倍率", changedBy: document.SystemSettings.Audit.ActorName);
        await LoadDrawPrizeAsync();
        await MessageService.Success($"已套用規則模板「{preset.Name}」。");
    }

    private async Task SaveLevelRatesAsync()
    {
        if (!TrySyncLevelRatesFromRows(out var errorMessage))
        {
            await MessageService.Warning(errorMessage ?? "倍率設定有重複或無效資料。");
            return;
        }

        await PersistLevelRatesAsync();
        await AuditService.RecordAsync("Settings", "更新抽獎倍率", "倍率設定", $"共 {levelRate.Count} 個等級", changedBy: (await ConfigStore.LoadAsync()).SystemSettings.Audit.ActorName);
        await LoadDrawPrizeAsync();
        await MessageService.Success("已儲存倍率設定。");
    }

    private async Task AddLevelRateRowAsync()
    {
        var level = newLevelName.Trim();
        if (string.IsNullOrWhiteSpace(level))
        {
            await MessageService.Warning("請輸入等級名稱。");
            return;
        }

        if (levelRateRows.Any(row => string.Equals(row.Level, level, StringComparison.OrdinalIgnoreCase)))
        {
            await MessageService.Warning("這個等級已經存在。");
            return;
        }

        levelRateRows.Add(new LevelRateRow
        {
            Level = level,
            Rate = Math.Max(1, newLevelRate)
        });

        newLevelName = "";
        newLevelRate = 1;
        await MessageService.Success($"已新增等級「{level}」。");
    }

    private async Task RemoveLevelRateRowAsync(string level)
    {
        var row = levelRateRows.FirstOrDefault(item => string.Equals(item.Level, level, StringComparison.OrdinalIgnoreCase));
        if (row == null)
        {
            return;
        }

        levelRateRows.Remove(row);
        await MessageService.Success($"已移除等級「{level}」。");
    }

    private async Task RemoveRulePresetAsync(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return;
        }

        var document = await ConfigStore.LoadAsync();
        var preset = document.DrawingRulePresets
            .FirstOrDefault(item => string.Equals(item.Name, presetName, StringComparison.OrdinalIgnoreCase));

        if (preset == null)
        {
            await MessageService.Warning("找不到指定的規則模板。");
            return;
        }

        document.DrawingRulePresets.Remove(preset);
        await ConfigStore.SaveAsync(document);
        await AuditService.RecordAsync("Template", "刪除規則模板", preset.Name, $"模板名稱 {preset.Name}", changedBy: document.SystemSettings.Audit.ActorName);
        await LoadRulePresetsAsync();

        if (string.Equals(selectedRulePresetName, presetName, StringComparison.OrdinalIgnoreCase))
        {
            selectedRulePresetName = drawingRulePresets.FirstOrDefault()?.Name ?? "";
        }

        await MessageService.Success($"已刪除規則模板「{presetName}」。");
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

    private void SetLevelRates(Dictionary<string, int> rates)
    {
        levelRate = new Dictionary<string, int>(rates, StringComparer.OrdinalIgnoreCase);
        SyncLevelRatesRowsFromDictionary();
    }

    private void SyncLevelRatesRowsFromDictionary()
    {
        levelRateRows = levelRate
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new LevelRateRow
            {
                Level = item.Key,
                Rate = Math.Max(1, item.Value)
            })
            .ToList();
    }

    private bool TrySyncLevelRatesFromRows(out string? errorMessage)
    {
        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in levelRateRows)
        {
            var level = row.Level.Trim();
            if (string.IsNullOrWhiteSpace(level))
            {
                continue;
            }

            if (normalized.ContainsKey(level))
            {
                errorMessage = $"等級「{level}」重複，請先修正再儲存。";
                return false;
            }

            normalized[level] = Math.Max(1, row.Rate);
        }

        levelRate = normalized;
        errorMessage = null;
        return true;
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

        if (changed)
        {
            SyncLevelRatesRowsFromDictionary();
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
