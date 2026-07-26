using System.Net.Http.Json;
using EasyLotteryDomain.Models.Config;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services;

public sealed class DonateLotteryActivityApiClient
{
    private const string AdminHeaderName = "X-EasyLottery-Admin-Token";

    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;

    public DonateLotteryActivityApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<IReadOnlyList<DonateLotteryActivity>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/donate-activities", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DonateLotteryActivity>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<DonateLotteryActivity> SaveAsync(DonateLotteryActivity activity, CancellationToken cancellationToken = default)
    {
        var method = activity.Id <= 0 ? HttpMethod.Post : HttpMethod.Put;
        var path = activity.Id <= 0 ? "api/donate-activities" : $"api/donate-activities/{activity.Id}";
        using var request = await CreateRequestAsync(method, path, cancellationToken);
        request.Content = JsonContent.Create(activity);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DonateLotteryActivity>(cancellationToken: cancellationToken) ?? activity;
    }

    public async Task DeleteAsync(int activityId, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Delete, $"api/donate-activities/{activityId}", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        var token = await _jsRuntime.InvokeAsync<string>("easyLotteryConfig.getAdminToken", cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Add(AdminHeaderName, token);
        }

        return request;
    }
}
