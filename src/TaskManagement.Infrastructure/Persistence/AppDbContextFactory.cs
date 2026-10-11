using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManagement.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(
                "Server=localhost,1433;Database=TaskManagement;User Id=sa;Password=TaskManager@2026;TrustServerCertificate=True;MultipleActiveResultSets=True")
            .Options;

        return new AppDbContext(options);
    }
}
