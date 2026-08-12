using EasyLotteryApi.Activities.Donate;
using EasyLotteryApi.Security;
using Results = Microsoft.AspNetCore.Http.Results;

namespace EasyLotteryApi.Activities.Donate.Endpoints;

internal static class AiCongratulationEndpoints
{
    public static void MapAiCongratulationEndpoints(this WebApplication app) => app.MapPost("/api/polaroid-congratulation", async (AiCongratulationRequest request, HttpContext context, ObsSessionAccess access, AiCongratulationProvider provider) =>
    {
        var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, ObsResourceKind.Donate, request.ActivityPublicId.ToString(), ObsSessionScope.Read));
        if (denied is not null) return denied;
        var message = await provider.GenerateAsync(request.DonorName, request.PrizeName, context.RequestAborted);
        return Results.Ok(new { message });
    });
}

internal sealed record AiCongratulationRequest(Guid ActivityPublicId, string DonorName, string PrizeName);
