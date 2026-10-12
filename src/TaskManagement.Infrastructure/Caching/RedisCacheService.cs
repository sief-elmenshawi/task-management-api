using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connection, ILogger<RedisCacheService> logger)
    {
        _connection = connection;
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

            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss for {Key}.", key);
                return default;
            }

            _logger.LogDebug("Cache hit for {Key}.", key);
            return JsonSerializer.Deserialize<T>(value.ToString(), SerializerOptions);
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

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (!_connection.IsConnected)
        {
            return false;
        }

        try
        {
            await _connection.GetDatabase().PingAsync();
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis health check failed.");
            return false;
        }
    }

    private IDatabase? GetDatabase() =>
        _connection.IsConnected ? _connection.GetDatabase() : null;
}
