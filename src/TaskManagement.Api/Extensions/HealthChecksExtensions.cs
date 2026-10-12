using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TaskManagement.Api.HealthChecks;

namespace TaskManagement.Api.Extensions;

public static class HealthChecksExtensions
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: [ReadyTag])
            .AddCheck<RedisHealthCheck>("redis", tags: [ReadyTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag)
        });

        return endpoints;
    }
}
