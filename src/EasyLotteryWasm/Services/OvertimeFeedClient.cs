using System.Net.Http.Json;
using EasyLotteryDomain.Models.Overtime;

namespace EasyLotteryWasm.Services
{
    public sealed class OvertimeFeedClient
    {
        private readonly HttpClient _httpClient;

        public OvertimeFeedClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<OvertimeSupportEvent>> GetEventsAsync(CancellationToken cancellationToken = default)
        {
            var events = await _httpClient.GetFromJsonAsync<List<OvertimeSupportEvent>>("api/overtime/events", cancellationToken);
            return events ?? [];
        }

        public async Task<OvertimeSupportEvent?> PostSuperChatAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("api/overtime/superchat", eventItem, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OvertimeSupportEvent>(cancellationToken);
        }

        public async Task<OvertimeSupportEvent?> PostEcpayDonateAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("api/overtime/ecpay", eventItem, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OvertimeSupportEvent>(cancellationToken);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await _httpClient.DeleteAsync("api/overtime/events", cancellationToken);
        }
    }
}
