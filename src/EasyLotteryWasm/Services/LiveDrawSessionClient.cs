using System.Net;
using System.Net.Http.Json;
using EasyLotteryDomain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace EasyLotteryWasm.Services;

public sealed class LiveDrawSessionClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;
    private readonly ObsSessionService _sessionService;
    private HubConnection? _connection;
    private string? _kind;
    private Guid _publicId;

    public event Func<LiveDrawSessionState, Task>? StateChanged;

    public LiveDrawSessionClient(HttpClient httpClient, NavigationManager navigationManager, ObsSessionService sessionService)
    {
        _httpClient = httpClient;
        _navigationManager = navigationManager;
        _sessionService = sessionService;
    }

    public async Task<LiveDrawSessionState?> StartAsync(string kind, Guid publicId, CancellationToken cancellationToken = default)
    {
        _kind = kind;
        _publicId = publicId;
        if (_connection is null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_navigationManager.ToAbsoluteUri("/hubs/live-draw"))
                .WithAutomaticReconnect()
                .Build();
            _connection.On<LiveDrawSessionState>(LiveDrawSessionServiceEventNames.StateChanged, NotifyAsync);
            _connection.Reconnected += async _ =>
            {
                await JoinAsync(CancellationToken.None);
                var state = await GetStateAsync(CancellationToken.None);
                if (state is not null) await NotifyAsync(state);
            };
            await _connection.StartAsync(cancellationToken);
        }

        await JoinAsync(cancellationToken);
        return await GetStateAsync(cancellationToken);
    }

    public Task<LiveDrawSessionState> PokeAsync(PokeLiveDrawCommand command, CancellationToken cancellationToken = default) =>
        PostAsync($"api/live-draw/pokebox/{_publicId:D}/poke", command, cancellationToken);

    public Task<LiveDrawSessionState> ResetPokeAsync(CancellationToken cancellationToken = default) =>
        PostAsync($"api/live-draw/pokebox/{_publicId:D}/reset", new { }, cancellationToken);

    public Task<LiveDrawSessionState> SpinAsync(RouletteLiveDrawCommand command, CancellationToken cancellationToken = default) =>
        PostAsync($"api/live-draw/roulette/{_publicId:D}/spin", command, cancellationToken);

    public async Task<LiveDrawSessionState?> GetStateAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/live-draw/{_kind}/{_publicId:D}", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<LiveDrawSessionState>(cancellationToken: cancellationToken);
    }

    private async Task<LiveDrawSessionState> PostAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = await CreateRequestAsync(HttpMethod.Post, path, cancellationToken);
        request.Content = JsonContent.Create(body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<LiveDrawSessionState>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("即時抽獎服務未回傳狀態。");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ObsSessionTokenHeader.Name, await _sessionService.GetSessionTokenAsync(cancellationToken));
        return request;
    }

    private async Task JoinAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (_connection is not null)
        {
            var token = await _sessionService.GetSessionTokenAsync(cancellationToken);
            await _connection.InvokeAsync("JoinSession", _kind, _publicId, token, cancellationToken);
        }
    }

    private async Task NotifyAsync(LiveDrawSessionState state)
    {
        var handlers = StateChanged;
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList().Cast<Func<LiveDrawSessionState, Task>>())
        {
            await handler(state);
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_kind) || _publicId == Guid.Empty)
        {
            throw new InvalidOperationException("尚未設定即時抽獎工作階段。");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: cancellationToken);
        throw new InvalidOperationException(error?.Error ?? $"即時抽獎服務失敗 ({(int)response.StatusCode})。");
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    private sealed record ApiError(string Error);
}

internal static class LiveDrawSessionServiceEventNames
{
    public const string StateChanged = "LiveDrawStateChanged";
}
