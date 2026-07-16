using EasyLotteryDomain.Models.Overtime;
using EasyLotteryDomain.Services;

namespace EasyLotteryWasm.Services
{
    public sealed class OvertimeFeedClient
    {
        private readonly IOvertimeFeedStore _store;

        public OvertimeFeedClient(IOvertimeFeedStore store)
        {
            _store = store;
        }

        public Task<IReadOnlyList<OvertimeSupportEvent>> GetEventsAsync(CancellationToken cancellationToken = default)
        {
            return _store.SnapshotAsync();
        }

        public async Task<OvertimeSupportEvent?> PostSuperChatAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken = default)
        {
            eventItem.Source = OvertimeSupportSource.SuperChat;
            eventItem.SourceLabel = string.IsNullOrWhiteSpace(eventItem.SourceLabel) ? "YouTube SuperChat" : eventItem.SourceLabel;
            return await _store.AddAsync(eventItem);
        }

        public async Task<OvertimeSupportEvent?> PostEcpayDonateAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken = default)
        {
            eventItem.Source = OvertimeSupportSource.EcpayDonate;
            eventItem.SourceLabel = string.IsNullOrWhiteSpace(eventItem.SourceLabel) ? "ECPay Donate" : eventItem.SourceLabel;
            return await _store.AddAsync(eventItem);
        }

        public async Task<OvertimeSupportEvent?> PostManualAsync(OvertimeSupportEvent eventItem, CancellationToken cancellationToken = default)
        {
            eventItem.Source = OvertimeSupportSource.Manual;
            eventItem.SourceLabel = string.IsNullOrWhiteSpace(eventItem.SourceLabel) ? "加班開始" : eventItem.SourceLabel;
            return await _store.AddAsync(eventItem);
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return _store.ClearAsync();
        }
    }
}
