using System.Text.Json;
using EasyLotteryDomain.Models.Overtime;
using EasyLotteryDomain.Services;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services
{
    public sealed class BrowserOvertimeFeedStore : IOvertimeFeedStore
    {
        private const int MaxEvents = 30;
        private readonly IJSRuntime _jsRuntime;

        public BrowserOvertimeFeedStore(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<IReadOnlyList<OvertimeSupportEvent>> SnapshotAsync()
        {
            var json = await _jsRuntime.InvokeAsync<string>("easyLotteryOvertimeFeed.read") ?? "[]";
            return DeserializeEvents(json);
        }

        public async Task<OvertimeSupportEvent> AddAsync(OvertimeSupportEvent entry)
        {
            var normalized = Normalize(entry);
            var json = await _jsRuntime.InvokeAsync<string>("easyLotteryOvertimeFeed.read") ?? "[]";
            var events = DeserializeEvents(json).ToList();
            events.Insert(0, normalized);

            if (events.Count > MaxEvents)
            {
                events.RemoveRange(MaxEvents, events.Count - MaxEvents);
            }

            await _jsRuntime.InvokeVoidAsync("easyLotteryOvertimeFeed.write", JsonSerializer.Serialize(events));
            return normalized;
        }

        public async Task ClearAsync()
        {
            await _jsRuntime.InvokeVoidAsync("easyLotteryOvertimeFeed.clear");
        }

        private static IReadOnlyList<OvertimeSupportEvent> DeserializeEvents(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<OvertimeSupportEvent>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static OvertimeSupportEvent Normalize(OvertimeSupportEvent entry)
        {
            entry.Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id;
            entry.DisplayName = entry.DisplayName?.Trim() ?? "";
            entry.Message = entry.Message?.Trim() ?? "";
            entry.AmountDisplay = entry.AmountDisplay?.Trim() ?? "";
            entry.Currency = string.IsNullOrWhiteSpace(entry.Currency) ? "TWD" : entry.Currency.Trim();
            entry.SourceLabel = string.IsNullOrWhiteSpace(entry.SourceLabel) ? entry.Source.ToString() : entry.SourceLabel.Trim();
            entry.Color = string.IsNullOrWhiteSpace(entry.Color) ? "#ff85b4" : entry.Color.Trim();
            entry.AvatarUrl = entry.AvatarUrl?.Trim() ?? "";
            entry.ExternalId = entry.ExternalId?.Trim();
            entry.OccurredAtUtc = entry.OccurredAtUtc == default ? DateTimeOffset.UtcNow : entry.OccurredAtUtc;
            return entry;
        }
    }
}
