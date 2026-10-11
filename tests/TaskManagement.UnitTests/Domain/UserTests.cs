using FluentAssertions;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void NewUser_IsNotDeleted()
    {
        var user = new User("Jane", "jane@example.com", "hash", UserRole.User);

        user.IsDeleted.Should().BeFalse();
        user.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsDeleted_SetsFlags()
    {
        var user = new User("Jane", "jane@example.com", "hash", UserRole.User);

        user.MarkAsDeleted();

        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsDeleted_IsIdempotent_AndKeepsOriginalTimestamp()
    {
        var user = new User("Jane", "jane@example.com", "hash", UserRole.User);
        user.MarkAsDeleted();
        var firstDeletedAt = user.DeletedAt;

        user.MarkAsDeleted();

        user.DeletedAt.Should().Be(firstDeletedAt);
    }
}
