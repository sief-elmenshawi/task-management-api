using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Api.HealthChecks;

public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public SqlServerHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("SQL Server is not reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server connectivity check failed.", exception);
        }
    }
}
