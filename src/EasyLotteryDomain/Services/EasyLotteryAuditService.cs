using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services
{
    public sealed class EasyLotteryAuditService
    {
        private const int MaxAuditRecords = 200;

        private readonly IEasyLotteryConfigStore _configStore;

        public EasyLotteryAuditService(IEasyLotteryConfigStore configStore)
        {
            _configStore = configStore;
        }

        public async Task<List<ChangeAuditRecord>> ListAsync(CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            return document.AuditRecords
                .OrderByDescending(record => record.ChangedAtUtc)
                .ThenByDescending(record => record.Id)
                .ToList();
        }

        public async Task RecordAsync(
            string category,
            string action,
            string targetName,
            string details = "",
            string? changedBy = null,
            CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);
            var actorName = string.IsNullOrWhiteSpace(changedBy)
                ? document.SystemSettings?.Audit?.ActorName
                : changedBy;

            var record = new ChangeAuditRecord
            {
                Id = document.IdSequence.NextAuditRecordId++,
                ChangedAtUtc = DateTime.UtcNow,
                ChangedBy = string.IsNullOrWhiteSpace(actorName) ? "本機操作" : actorName.Trim(),
                Category = category.Trim(),
                Action = action.Trim(),
                TargetName = targetName.Trim(),
                Details = details.Trim()
            };

            document.AuditRecords.Add(record);
            if (document.AuditRecords.Count > MaxAuditRecords)
            {
                document.AuditRecords = document.AuditRecords
                    .OrderByDescending(item => item.ChangedAtUtc)
                    .ThenByDescending(item => item.Id)
                    .Take(MaxAuditRecords)
                    .OrderBy(item => item.ChangedAtUtc)
                    .ThenBy(item => item.Id)
                    .ToList();
            }

            await _configStore.SaveAsync(document, cancellationToken);
        }
    }
}
