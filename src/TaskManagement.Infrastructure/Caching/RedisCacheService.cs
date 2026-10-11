using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly object _lock = new();

    private IConnectionMultiplexer? _connection;

    public RedisCacheService(string connectionString, ILogger<RedisCacheService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = GetDatabase();
            if (database is null)
            {
                return default;
            }

            var value = await database.StringGetAsync(key);
            return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString(), SerializerOptions);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to read cache key {Key}; falling back to the database.",
                key);

            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = GetDatabase();
            if (database is null)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            var expiration = expiry is { } ttl ? (Expiration)ttl : Expiration.Default;
            await database.StringSetAsync(key, payload, expiration);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to write cache key {Key}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = GetDatabase();
            if (database is null)
            {
                return;
            }

            await database.KeyDeleteAsync(key);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to evict cache key {Key}.", key);
        }
    }

    private IDatabase? GetDatabase() => GetConnection()?.GetDatabase();

    private IConnectionMultiplexer? GetConnection()
    {
        if (_connection is not null)
        {
            return _connection;
        }

        lock (_lock)
        {
            if (_connection is not null)
            {
                return _connection;
            }

            try
            {
                var options = ConfigurationOptions.Parse(_connectionString);
                options.AbortOnConnectFail = false;

                _connection = ConnectionMultiplexer.Connect(options);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not connect to Redis at {ConnectionString}. Caching is disabled and requests fall back to the database.",
                    _connectionString);

                _connection = null;
            }

            return _connection;
        }
    }
}
