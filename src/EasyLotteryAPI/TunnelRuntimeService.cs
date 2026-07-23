using System.Diagnostics;
using System.Text.RegularExpressions;

public sealed class TunnelRuntimeService : IAsyncDisposable
{
    private static readonly Regex PublicUrlPattern = new(@"https://[^\s""']+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private TunnelRuntimeStatus _status = new();

    public TunnelRuntimeService(IConfiguration configuration) => _configuration = configuration;

    public TunnelRuntimeStatus GetStatus() => _status;

    public async Task<TunnelRuntimeStatus> StartAsync(string provider, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopInternalAsync();
            var port = _configuration.GetValue<int?>("Tunnel:LocalPort") ?? 18930;
            var normalizedProvider = provider?.Trim().ToLowerInvariant();
            var (command, arguments) = normalizedProvider switch
            {
                "cloudflare-quick" => (_configuration["Tunnel:CloudflareCommand"] ?? "cloudflared", $"tunnel --url http://127.0.0.1:{port}"),
                "dev-tunnels" => (_configuration["Tunnel:DevTunnelsCommand"] ?? "devtunnel", $"host -p {port} --allow-anonymous"),
                _ => throw new InvalidOperationException("Unsupported tunnel provider.")
            };

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
            _status = new TunnelRuntimeStatus { Provider = normalizedProvider, State = "starting" };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                var publicUrl = await urlReady.Task.WaitAsync(timeout.Token);
                _status = new TunnelRuntimeStatus { Provider = normalizedProvider, State = "running", PublicBaseUrl = publicUrl };
            }
            catch (OperationCanceledException)
            {
                _status = new TunnelRuntimeStatus
                {
                    Provider = normalizedProvider,
                    State = process.HasExited ? "failed" : "starting",
                    Error = process.HasExited ? $"{command} exited before returning a public URL." : "Tunnel is starting; check its CLI login and network access."
                };
            }

            return _status;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _status = new TunnelRuntimeStatus { Provider = provider ?? "", State = "failed", Error = ex.Message };
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
        _status = new TunnelRuntimeStatus();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
}

public sealed class TunnelRuntimeStatus
{
    public string Provider { get; init; } = "";
    public string State { get; init; } = "stopped";
    public string PublicBaseUrl { get; init; } = "";
    public string Error { get; init; } = "";
}

public sealed class TunnelStartRequest
{
    public string Provider { get; set; } = "";
}
