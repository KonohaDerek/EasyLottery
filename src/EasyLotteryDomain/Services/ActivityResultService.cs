using System.Text.Json;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryDomain.Services
{
    public sealed class ActivityResultService
    {
        private static readonly JsonSerializerOptions ExportJsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly IEasyLotteryConfigStore _configStore;

        public ActivityResultService(IEasyLotteryConfigStore configStore)
        {
            _configStore = configStore;
        }

        public async Task<List<ActivityResultRecord>> ListActivityResultsAsync(CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            return document.ActivityResults
                .OrderByDescending(record => record.ActivityDateUtc)
                .ThenByDescending(record => record.Id)
                .ToList();
        }

        public static List<ActivityResultRecord> FilterActivityResults(
            IEnumerable<ActivityResultRecord> records,
            string? searchText = null,
            ActivityResultType? activityType = null,
            DateOnly? startDate = null,
            DateOnly? endDate = null)
        {
            var query = records ?? Enumerable.Empty<ActivityResultRecord>();

            if (activityType.HasValue)
            {
                query = query.Where(record => record.ActivityType == activityType.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(record => DateOnly.FromDateTime(record.ActivityDateUtc.ToLocalTime()) >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(record => DateOnly.FromDateTime(record.ActivityDateUtc.ToLocalTime()) <= endDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var normalizedSearch = searchText.Trim();
                query = query.Where(record => MatchesSearch(record, normalizedSearch));
            }

            return query
                .OrderByDescending(record => record.ActivityDateUtc)
                .ThenByDescending(record => record.Id)
                .ToList();
        }

        public static string SerializeActivityResults(IEnumerable<ActivityResultRecord> records)
        {
            return JsonSerializer.Serialize(records, ExportJsonOptions);
        }

        public async Task<ActivityResultRecord?> LoadActivityResultAsync(int id, CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            return document.ActivityResults.FirstOrDefault(record => record.Id == id);
        }

        public async Task<ActivityResultRecord> RecordPokeActivityAsync(int templateId, CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            var template = document.PokeTemplates.FirstOrDefault(item => item.Id == templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            var revealedCells = template.Cells
                .Where(cell => cell.IsRevealed)
                .OrderBy(cell => cell.RevealedAt ?? DateTime.MaxValue)
                .ThenBy(cell => cell.Index)
                .ToList();

            if (revealedCells.Count == 0)
            {
                throw new InvalidOperationException("目前沒有可記錄的戳戳樂結果。");
            }

            var now = DateTime.UtcNow;
            var record = new ActivityResultRecord
            {
                Id = document.IdSequence.NextActivityResultId++,
                ActivityType = ActivityResultType.PokeBox,
                ActivityName = template.Name,
                TemplateId = template.Id,
                ActivityDateUtc = now,
                Summary = $"已揭曉 {revealedCells.Count} / {template.Cells.Count} 格",
                Items = revealedCells
                    .Select((cell, index) => new ActivityResultItem
                    {
                        Order = index + 1,
                        Name = cell.Title,
                        Description = string.IsNullOrWhiteSpace(cell.SubTitle) ? $"第 {cell.Index + 1} 格" : cell.SubTitle,
                        ImageUrl = !string.IsNullOrWhiteSpace(cell.RevealedImageUrl) ? cell.RevealedImageUrl : cell.ImageUrl,
                        Color = cell.RevealedColor,
                        ResultedAtUtc = cell.RevealedAt
                    })
                    .ToList()
            };

            document.ActivityResults.Add(record);
            await _configStore.SaveAsync(document, cancellationToken);
            return record;
        }

        public async Task<ActivityResultRecord> RecordRouletteActivityAsync(int templateId, SpinResult spinResult, CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            var template = document.RouletteTemplates.FirstOrDefault(item => item.Id == templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            var segment = template.Segments
                .OrderBy(item => item.Index)
                .FirstOrDefault(item => item.Index == spinResult.SegmentIndex);

            var segmentTitle = segment?.Title ?? spinResult.SegmentTitle;
            if (string.IsNullOrWhiteSpace(segmentTitle))
            {
                throw new InvalidOperationException("目前沒有可記錄的轉盤結果。");
            }

            var record = new ActivityResultRecord
            {
                Id = document.IdSequence.NextActivityResultId++,
                ActivityType = ActivityResultType.Roulette,
                ActivityName = template.Name,
                TemplateId = template.Id,
                ActivityDateUtc = DateTime.UtcNow,
                Summary = $"中獎項目：{segmentTitle}",
                Items = new List<ActivityResultItem>
                {
                    new()
                    {
                        Order = 1,
                        Name = segmentTitle,
                        Description = segment == null ? "中獎結果" : $"第 {segment.Index + 1} 格",
                        ImageUrl = segment?.ImageUrl ?? "",
                        Color = segment?.Color ?? "",
                        ResultedAtUtc = DateTime.UtcNow
                    }
                }
            };

            document.ActivityResults.Add(record);
            await _configStore.SaveAsync(document, cancellationToken);
            return record;
        }

        private static bool MatchesSearch(ActivityResultRecord record, string searchText)
        {
            if (Contains(record.ActivityName, searchText) ||
                Contains(record.Summary, searchText) ||
                Contains(record.ActivityType.ToString(), searchText))
            {
                return true;
            }

            return record.Items.Any(item =>
                Contains(item.Name, searchText) ||
                Contains(item.Description, searchText));
        }

        private static bool Contains(string? source, string searchText)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }
    }
}
