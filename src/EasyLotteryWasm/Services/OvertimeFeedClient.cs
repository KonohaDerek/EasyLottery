using EasyLotteryDomain.Models.Overtime;
using System.Net.Http.Json;

namespace EasyLotteryWasm.Services
{
    public sealed class OvertimeFeedClient
    {
        private const int MaxEvents = 30;
        private readonly HttpClient _httpClient;
        private readonly ObsSessionService _sessionService;

        public OvertimeFeedClient(HttpClient httpClient, ObsSessionService sessionService)
        {
            _httpClient = httpClient;
            _sessionService = sessionService;
        }

        public async Task<IReadOnlyList<OvertimeSupportEvent>> GetEventsAsync(CancellationToken cancellationToken = default)
        {
            using var request = await CreateRequestAsync(HttpMethod.Get, "api/overtime-feed", cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<OvertimeSupportEvent>>(cancellationToken: cancellationToken) ?? [];
        }

        public Task<OvertimeSupportEvent?> PostSuperChatAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken = default)
        {
            eventItem.Source = OvertimeSupportSource.SuperChat;
            eventItem.SourceLabel = string.IsNullOrWhiteSpace(eventItem.SourceLabel) ? "YouTube SuperChat" : eventItem.SourceLabel;
            return AddAsync(eventItem, cancellationToken);
        }

        public Task<OvertimeSupportEvent?> PostEcpayDonateAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken = default)
        {
            eventItem.Source = OvertimeSupportSource.EcpayDonate;
            eventItem.SourceLabel = string.IsNullOrWhiteSpace(eventItem.SourceLabel) ? "ECPay Donate" : eventItem.SourceLabel;
            return AddAsync(eventItem, cancellationToken);
        }

        public Task<OvertimeSupportEvent?> PostManualAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken = default)
        {
            eventItem.Source = OvertimeSupportSource.Manual;
            eventItem.SourceLabel = string.IsNullOrWhiteSpace(eventItem.SourceLabel) ? "加班開始" : eventItem.SourceLabel;
            return AddAsync(eventItem, cancellationToken);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            using var request = await CreateRequestAsync(HttpMethod.Delete, "api/overtime-feed", cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task<OvertimeSupportEvent?> AddAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken)
        {
            eventItem = Normalize(eventItem);
            var events = (await GetEventsAsync(cancellationToken)).ToList();
            events.Insert(0, eventItem);
            if (events.Count > MaxEvents)
            {
                events.RemoveRange(MaxEvents, events.Count - MaxEvents);
            }

            using var request = await CreateRequestAsync(HttpMethod.Put, "api/overtime-feed", cancellationToken);
            request.Content = JsonContent.Create(events);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return eventItem;
        }

        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-EasyLottery-Session-Token", await _sessionService.GetSessionTokenAsync(cancellationToken));
            return request;
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
