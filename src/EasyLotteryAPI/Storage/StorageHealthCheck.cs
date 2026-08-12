using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EasyLotteryApi.Storage;

public sealed class StorageHealthCheck(IConfiguration configuration, IHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var directory = configuration["Storage:Directory"]
            ?? Path.Combine(environment.ContentRootPath, "App_Data");
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".health-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy("Storage directory is readable and writable."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage directory is not writable.", exception));
        }
    }
}
