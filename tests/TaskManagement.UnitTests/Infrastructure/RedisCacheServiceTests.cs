using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using TaskManagement.Infrastructure.Caching;

namespace TaskManagement.UnitTests.Infrastructure;

public class RedisCacheServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsDefault_AndDoesNotThrow_WhenMultiplexerIsDisconnected()
    {
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.IsConnected.Returns(false);

        var service = new RedisCacheService(connection, NullLogger<RedisCacheService>.Instance);

        var result = await service.GetAsync<object>("task:key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsFalse_WhenMultiplexerIsDisconnected()
    {
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.IsConnected.Returns(false);

        var service = new RedisCacheService(connection, NullLogger<RedisCacheService>.Instance);

        (await service.IsHealthyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SetIfNotExistsAsync_DoesNotThrow_WhenMultiplexerIsDisconnected()
    {
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.IsConnected.Returns(false);

        var service = new RedisCacheService(connection, NullLogger<RedisCacheService>.Instance);

        var act = () => service.SetIfNotExistsAsync("task:key", new { Id = 1 });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveAsync_QueuesInvalidation_WhileDisconnected_AndRetriesAfterReconnect()
    {
        var connection = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        connection.IsConnected.Returns(false);
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);

        var service = new RedisCacheService(connection, NullLogger<RedisCacheService>.Instance);

        await service.RemoveAsync("task:key");

        await database.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());

        connection.IsConnected.Returns(true);
        database.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(true);

        await service.GetAsync<object>("other:key");

        await database.Received(1).KeyDeleteAsync("task:key", Arg.Any<CommandFlags>());
    }
}