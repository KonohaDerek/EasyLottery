using System.Collections.Concurrent;

namespace EasyLotteryInfrastructure.Storage;

public sealed class StorageGateProvider : IStorageGateProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default, params string[] paths)
    {
        var normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var acquired = new List<SemaphoreSlim>(normalizedPaths.Length);
        try
        {
            foreach (var path in normalizedPaths)
            {
                var gate = _gates.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(cancellationToken);
                acquired.Add(gate);
            }

            return new Releaser(acquired);
        }
        catch
        {
            for (var i = acquired.Count - 1; i >= 0; i--)
            {
                acquired[i].Release();
            }

            throw;
        }
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly IReadOnlyList<SemaphoreSlim> _gates;
        private bool _disposed;

        public Releaser(IReadOnlyList<SemaphoreSlim> gates) => _gates = gates;

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            for (var i = _gates.Count - 1; i >= 0; i--)
            {
                _gates[i].Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
