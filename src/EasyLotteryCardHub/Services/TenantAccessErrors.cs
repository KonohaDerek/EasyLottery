using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;

namespace EasyLotteryCardHub.Services;

public static class TenantAccessErrors
{
    public static IResult ForStatus(TenantStatus status)
    {
        var exception = AsException(status);
        return HubErrorResults.Forbidden(exception.Code, exception.Message);
    }

    public static HubServiceException AsException(TenantStatus status) => status switch
    {
        TenantStatus.Disabled => new HubServiceException(StatusCodes.Status403Forbidden, "tenant_disabled", "This tenant is disabled."),
        TenantStatus.PendingDeletion => new HubServiceException(StatusCodes.Status403Forbidden, "tenant_pending_deletion", "This tenant is pending deletion."),
        TenantStatus.Deleted => new HubServiceException(StatusCodes.Status403Forbidden, "tenant_deleted", "This tenant has been deleted."),
        _ => new HubServiceException(StatusCodes.Status403Forbidden, "tenant_unavailable", "This tenant is unavailable.")
    };
}
