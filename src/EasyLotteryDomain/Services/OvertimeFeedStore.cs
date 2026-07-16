using System.Threading.Tasks;
using EasyLotteryDomain.Models.Overtime;

namespace EasyLotteryDomain.Services
{
    public sealed class OvertimeFeedStore : IOvertimeFeedStore
    {
        private readonly object _gate = new();
        private readonly List<OvertimeSupportEvent> _events = new();
        private const int MaxEvents = 30;

        public Task<IReadOnlyList<OvertimeSupportEvent>> SnapshotAsync()
        {
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<OvertimeSupportEvent>>(_events
                    .OrderByDescending(item => item.OccurredAtUtc)
                    .ToList());
            }
        }

        public Task<OvertimeSupportEvent> AddAsync(OvertimeSupportEvent entry)
        {
            lock (_gate)
            {
                var normalized = Normalize(entry);
                _events.Insert(0, normalized);
                if (_events.Count > MaxEvents)
                {
                    _events.RemoveRange(MaxEvents, _events.Count - MaxEvents);
                }

                return Task.FromResult(normalized);
            }
        }

        public Task ClearAsync()
        {
            lock (_gate)
            {
                _events.Clear();
            }

            return Task.CompletedTask;
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
