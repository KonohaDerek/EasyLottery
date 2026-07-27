using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services;

public sealed class ObsSessionService
{
    private readonly IJSRuntime _jsRuntime;
    private string? _sessionToken;

    public ObsSessionService(IJSRuntime jsRuntime) => _jsRuntime = jsRuntime;

    public async Task<string> GetSessionTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_sessionToken))
        {
            return _sessionToken;
        }

        _sessionToken = (await _jsRuntime.InvokeAsync<string>("easyLotteryConfig.getSessionToken", cancellationToken)).Trim();
        return _sessionToken;
    }
}
