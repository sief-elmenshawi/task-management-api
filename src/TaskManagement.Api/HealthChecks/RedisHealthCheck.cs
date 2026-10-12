using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Api.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly ICacheService _cache;

    public RedisHealthCheck(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cache.IsHealthyAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Redis is reachable.")
                : HealthCheckResult.Degraded("Redis is not reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded("Redis connectivity check failed.", exception);
        }
    }
}
