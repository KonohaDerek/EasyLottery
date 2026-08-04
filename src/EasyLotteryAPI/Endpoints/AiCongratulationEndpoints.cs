namespace EasyLotteryApi.Endpoints;

internal static class AiCongratulationEndpoints
{
    public static void MapAiCongratulationEndpoints(this WebApplication app) => app.MapPost("/api/polaroid-congratulation", async (AiCongratulationRequest request, HttpContext context, ObsSessionAccess access, AiCongratulationProvider provider) =>
    {
        var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, "donate", request.ActivityPublicId.ToString(), "read"));
        if (denied is not null) return denied;
        var message = await provider.GenerateAsync(request.DonorName, request.PrizeName, context.RequestAborted);
        return Results.Ok(new { message });
    });
}

internal sealed record AiCongratulationRequest(Guid ActivityPublicId, string DonorName, string PrizeName);
