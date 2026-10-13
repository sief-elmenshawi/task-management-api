using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace TaskManagement.IntegrationTests;

/// <summary>
/// Boots the real API through <see cref="WebApplicationFactory{TEntryPoint}"/> against a throwaway
/// SQL Server and Redis container. Containers are started lazily on the first
/// <see cref="CreateClientAsync"/> call so a skipped run (see <see cref="IntegrationFactAttribute"/>)
/// never touches Docker.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private WebApplicationFactory<Program>? _factory;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task<HttpClient> CreateClientAsync()
    {
        await EnsureStartedAsync();
        return _factory!.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();

        await _sql.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private async Task EnsureStartedAsync()
    {
        if (_factory is not null)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_factory is not null)
            {
                return;
            }

            await Task.WhenAll(_sql.StartAsync(), _redis.StartAsync());

            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _sql.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
                ["JwtSettings:Key"] = "integration-tests-signing-key-0123456789abcdef",
                ["RateLimiting:Auth:PermitLimit"] = "1000",
                ["RefreshToken:ReuseGraceSeconds"] = "0",
                ["Worker:RecoveryIntervalSeconds"] = "3600",
                ["Worker:RecoveryMinAgeSeconds"] = "0"
            };

            // The API layers appsettings.Local.json on top of appsettings.json, so an
            // app-configuration override added by the factory can be shadowed. Environment
            // variables are added to the builder after the JSON files and therefore win.
            foreach (var (key, value) in settings)
            {
                Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);
            }

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

            using var warmUp = _factory.CreateClient();
        }
        finally
        {
            _gate.Release();
        }
    }
}
