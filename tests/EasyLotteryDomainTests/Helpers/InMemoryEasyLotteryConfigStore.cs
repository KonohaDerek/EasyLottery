using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

namespace EasyLotteryDomainTests.Helpers
{
    internal sealed class InMemoryEasyLotteryConfigStore : IEasyLotteryConfigStore
    {
        private EasyLotteryConfigDocument _document = new();

        public Task<EasyLotteryConfigDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_document);
        }

        public Task SaveAsync(EasyLotteryConfigDocument document, CancellationToken cancellationToken = default)
        {
            _document = document;
            return Task.CompletedTask;
        }
    }
}