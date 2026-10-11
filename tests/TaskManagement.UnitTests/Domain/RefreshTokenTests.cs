using FluentAssertions;
using TaskManagement.Domain.Entities;

namespace TaskManagement.UnitTests.Domain;

public class RefreshTokenTests
{
    [Fact]
    public void IsActive_IsTrue_ForFreshToken()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(1));

        token.IsActive.Should().BeTrue();
        token.IsRevoked.Should().BeFalse();
        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_IsTrue_WhenExpiryIsInThePast()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddSeconds(-1));

        token.IsExpired.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_SetsRevokedAtAndReplacementHash()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(1));

        token.Revoke("replacement-hash");

        token.IsRevoked.Should().BeTrue();
        token.IsActive.Should().BeFalse();
        token.RevokedAt.Should().NotBeNull();
        token.ReplacedByTokenHash.Should().Be("replacement-hash");
    }
}
