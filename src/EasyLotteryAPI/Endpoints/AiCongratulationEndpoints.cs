namespace EasyLotteryApi.Endpoints;

internal static class AiCongratulationEndpoints
{
    public static void MapAiCongratulationEndpoints(this WebApplication app) => app.MapPost("/api/polaroid-congratulation", async (AiCongratulationRequest request, HttpContext context, ObsSessionAccess access, AiCongratulationProvider provider) =>
    {
        if (!access.IsAuthorized(context.Request)) return Results.Unauthorized();
        var message = await provider.GenerateAsync(request.DonorName, request.PrizeName, context.RequestAborted);
        return Results.Ok(new { message });
    });
}

internal sealed record AiCongratulationRequest(string DonorName, string PrizeName);
