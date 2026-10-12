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
}