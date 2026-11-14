using System.Text.Json;
using EasyLotteryCardHub.Data;

namespace EasyLotteryCardHub.Services;

public static class HubAuditWriter
{
    public static void Append(
        HubDbContext db,
        TimeProvider timeProvider,
        string eventType,
        string actorType,
        string actorId,
        Guid? tenantId,
        object details)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = eventType,
            ActorType = actorType,
            ActorId = actorId,
            TenantId = tenantId,
            CreatedAt = timeProvider.GetUtcNow(),
            DetailsJson = JsonSerializer.Serialize(details)
        });
    }
}
