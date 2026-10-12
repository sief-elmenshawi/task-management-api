using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Authentication;
using TaskManagement.Domain.Repositories;
using TaskManagement.Infrastructure.Authentication;
using TaskManagement.Infrastructure.Caching;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Persistence.Repositories;
using TaskManagement.Infrastructure.Queues;
using TaskManagement.Infrastructure.Workers;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    private const string DevSigningKey = "dev-only-signing-key-change-in-production-0123456789abcdef";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.Key),
                "JwtSettings:Key is required.")
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.Issuer),
                "JwtSettings:Issuer is required.")
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.Audience),
                "JwtSettings:Audience is required.")
            .Validate(
                settings => settings.ExpiryMinutes > 0,
                "JwtSettings:ExpiryMinutes must be greater than zero.")
            .Validate(
                settings => Encoding.UTF8.GetByteCount(settings.Key) >= 32,
                "JwtSettings:Key must be at least 32 bytes (256 bits) for HS256.")
            .PostConfigure<ILoggerFactory>((settings, loggerFactory) =>
            {
                if (string.Equals(settings.Key, DevSigningKey, StringComparison.Ordinal))
                {
                    loggerFactory.CreateLogger(typeof(JwtSettings)).LogWarning(
                        "JwtSettings:Key is the development-only default. Replace it with a unique, at-least-32-byte secret in any shared or production environment.");
                }
            })
            .ValidateOnStart();

        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .Validate(
                options => options.ReuseGraceSeconds >= 0,
                "RefreshToken:ReuseGraceSeconds must be at least zero.")
            .ValidateOnStart();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var multiplexerOptions = ConfigurationOptions.Parse(redisConnectionString);
            multiplexerOptions.AbortOnConnectFail = false;
            multiplexerOptions.ConnectTimeout = 500;
            multiplexerOptions.SyncTimeout = 500;
            multiplexerOptions.AsyncTimeout = 500;
            return ConnectionMultiplexer.Connect(multiplexerOptions);
        });

        services.AddSingleton<ICacheService>(provider => new RedisCacheService(
            provider.GetRequiredService<IConnectionMultiplexer>(),
            provider.GetRequiredService<ILogger<RedisCacheService>>()));

        services.AddSingleton<ChannelTaskQueue>();
        services.AddSingleton<ITaskProcessingQueue>(provider => provider.GetRequiredService<ChannelTaskQueue>());
        services.AddSingleton<ITransientFailureDetector, TransientSqlFailureDetector>();

        services.AddOptions<WorkerOptions>()
            .Bind(configuration.GetSection(WorkerOptions.SectionName))
            .Validate(
                options => options.Concurrency >= 1,
                "Worker:Concurrency must be at least 1.")
            .Validate(
                options => options.RecoveryIntervalSeconds >= 1,
                "Worker:RecoveryIntervalSeconds must be at least 1.")
            .Validate(
                options => options.RecoveryMinAgeSeconds >= 0,
                "Worker:RecoveryMinAgeSeconds must be at least 0.")
            .ValidateOnStart();

        services.AddHostedService<TaskProcessingWorker>();

        return services;
    }
}
