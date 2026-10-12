using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, byte> _pendingInvalidations = new();

    public RedisCacheService(IConnectionMultiplexer connection, ILogger<RedisCacheService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await FlushPendingInvalidationsAsync();

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
            await FlushPendingInvalidationsAsync();

            var database = GetDatabase();
            if (database is null)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await database.StringSetAsync(key, payload, expiry, When.Always);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to write cache key {Key}; the database is still authoritative.",
                key);
            _pendingInvalidations.TryAdd(key, default);
        }
    }

    public async Task SetIfNotExistsAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await FlushPendingInvalidationsAsync();

            var database = GetDatabase();
            if (database is null)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await database.StringSetAsync(key, payload, expiry, When.NotExists);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to populate cache key {Key} if absent; the database is still authoritative.",
                key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await FlushPendingInvalidationsAsync();

            var database = GetDatabase();
            if (database is null)
            {
                _pendingInvalidations.TryAdd(key, default);
                return;
            }

            await database.KeyDeleteAsync(key);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to delete cache key {Key}; the database is still authoritative.",
                key);
            _pendingInvalidations.TryAdd(key, default);
        }
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_connection.IsConnected);

    private IDatabase? GetDatabase() =>
        _connection.IsConnected ? _connection.GetDatabase() : null;

    private async Task FlushPendingInvalidationsAsync()
    {
        if (_pendingInvalidations.IsEmpty)
        {
            return;
        }

        var database = GetDatabase();
        if (database is null)
        {
            return;
        }

        foreach (var key in _pendingInvalidations.Keys)
        {
            try
            {
                if (await database.KeyDeleteAsync(key))
                {
                    _pendingInvalidations.TryRemove(key, out _);
                }
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Pending cache invalidation for {Key} failed; will retry.", key);
            }
        }
    }
}