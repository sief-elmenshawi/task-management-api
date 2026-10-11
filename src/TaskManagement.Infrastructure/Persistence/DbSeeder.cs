using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Infrastructure.Persistence;

public static class DbSeeder
{
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Admin@123";
    private const string AdminName = "Administrator";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync(cancellationToken);

        var adminExists = await context.Users.AnyAsync(u => u.Email == AdminEmail, cancellationToken);
        if (adminExists)
        {
            return;
        }

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var admin = new User(AdminName, AdminEmail, passwordHasher.Hash(AdminPassword), UserRole.Admin);

        await context.Users.AddAsync(admin, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
