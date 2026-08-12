using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace EasyLotteryApi.Tunnel;

public sealed class TunnelRuntimeService : IAsyncDisposable
{
    private static readonly Regex PublicUrlPattern = new(@"https://[^\s""']+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly IConfiguration _configuration;
    private readonly ILogger<TunnelRuntimeService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private TunnelRuntimeStatus _status = new();

    public TunnelRuntimeService(IConfiguration configuration, ILogger<TunnelRuntimeService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public TunnelRuntimeStatus GetStatus() => _status;

    public async Task<TunnelRuntimeStatus> StartAsync(TunnelProvider provider, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopInternalAsync();
            var port = _configuration.GetValue<int?>("Tunnel:LocalPort") ?? 18930;
            var normalizedProvider = provider.ToValue();
            var commands = new Dictionary<TunnelProvider, (string Command, string Arguments)>
            {
                [TunnelProvider.CloudflareQuick] = (_configuration["Tunnel:CloudflareCommand"] ?? "cloudflared", $"tunnel --url http://127.0.0.1:{port}"),
                [TunnelProvider.DevTunnels] = (_configuration["Tunnel:DevTunnelsCommand"] ?? "devtunnel", $"host -p {port} --allow-anonymous")
            };
            if (!commands.TryGetValue(provider, out var commandInfo))
                throw new InvalidOperationException("Unsupported tunnel provider.");
            var (command, arguments) = commandInfo;

            var startInfo = new ProcessStartInfo(command, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var urlReady = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            void ReadOutput(object? _, DataReceivedEventArgs eventArgs)
            {
                var match = PublicUrlPattern.Match(eventArgs.Data ?? string.Empty);
                if (match.Success)
                {
                    urlReady.TrySetResult(match.Value.TrimEnd('/', '.', ','));
                }
            }

            process.OutputDataReceived += ReadOutput;
            process.ErrorDataReceived += ReadOutput;
            if (!process.Start())
            {
                throw new InvalidOperationException($"Unable to start {command}.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;
            _status = new TunnelRuntimeStatus { Provider = normalizedProvider, State = TunnelRuntimeState.Starting.ToValue() };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                var publicUrl = await urlReady.Task.WaitAsync(timeout.Token);
                _status = new TunnelRuntimeStatus { Provider = normalizedProvider, State = TunnelRuntimeState.Running.ToValue(), PublicBaseUrl = publicUrl };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Tunnel provider {Provider} did not return a public URL before timeout/cancellation.", normalizedProvider);
                _status = new TunnelRuntimeStatus
                {
                    Provider = normalizedProvider,
                    State = (process.HasExited ? TunnelRuntimeState.Failed : TunnelRuntimeState.Starting).ToValue(),
                    Error = process.HasExited ? $"{command} exited before returning a public URL." : "Tunnel is starting; check its CLI login and network access."
                };
            }

            return _status;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to start tunnel provider {Provider}.", provider.ToValue());
            _status = new TunnelRuntimeStatus { Provider = provider.ToValue(), State = TunnelRuntimeState.Failed.ToValue(), Error = ex.Message };
            return _status;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await StopInternalAsync(); }
        finally { _gate.Release(); }
    }

    private Task StopInternalAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(3000);
        }
        _process?.Dispose();
        _process = null;
        _status = new TunnelRuntimeStatus { State = TunnelRuntimeState.Stopped.ToValue() };
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
}

public sealed class TunnelRuntimeStatus
{
    public string Provider { get; init; } = "";
    public string State { get; init; } = TunnelRuntimeState.Stopped.ToValue();
    public string PublicBaseUrl { get; init; } = "";
    public string Error { get; init; } = "";
}

public sealed class TunnelStartRequest
{
    public string Provider { get; set; } = "";
}
