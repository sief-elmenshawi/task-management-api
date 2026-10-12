using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Admin@123";
    private const string AdminName = "Administrator";
    private const int MaxMigrationAttempts = 10;

    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DatabaseInitializer));

        await MigrateWithRetryAsync(context, logger, cancellationToken);

        var adminExists = await context.Users.AnyAsync(u => u.Email == AdminEmail, cancellationToken);
        if (adminExists)
        {
            return;
        }

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var admin = new User(AdminName, AdminEmail, passwordHasher.Hash(AdminPassword), UserRole.Admin);

        await context.Users.AddAsync(admin, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded administrator account {AdminEmail}.", AdminEmail);
    }

    private static async Task MigrateWithRetryAsync(
        AppDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxMigrationAttempts; attempt++)
        {
            try
            {
                await context.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsTransientFailure(exception) && attempt < MaxMigrationAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));

                logger.LogWarning(
                    exception,
                    "Database migration attempt {Attempt}/{MaxAttempts} failed with a transient error; retrying in {DelaySeconds}s.",
                    attempt,
                    MaxMigrationAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsTransientFailure(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case SqlException { IsTransient: true }:
                case TimeoutException:
                case SocketException:
                    return true;
            }
        }

        return false;
    }
}
