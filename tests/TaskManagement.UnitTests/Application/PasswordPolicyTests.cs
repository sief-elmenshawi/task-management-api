using FluentAssertions;
using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Authentication.Validators;

namespace TaskManagement.UnitTests.Application;

public class PasswordPolicyTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();

    [Fact]
    public void Register_RejectsPasswordExceeding72Utf8Bytes()
    {
        var password = new string('é', 40) + "a1";

        var result = _registerValidator.Validate(
            new RegisterRequest("Jane", "jane@example.com", password, password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.ErrorMessage.Contains("72 UTF-8 bytes", StringComparison.Ordinal));
    }

    [Fact]
    public void Register_AcceptsPasswordAtOrBelow72Utf8Bytes()
    {
        var result = _registerValidator.Validate(
            new RegisterRequest("Jane", "jane@example.com", "Password1", "Password1"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Login_RejectsPasswordExceeding72Utf8Bytes()
    {
        var password = new string('é', 40) + "a1";

        var result = _loginValidator.Validate(new LoginRequest("jane@example.com", password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.ErrorMessage.Contains("72 UTF-8 bytes", StringComparison.Ordinal));
    }

    [Fact]
    public void Login_AcceptsPasswordAtOrBelow72Utf8Bytes()
    {
        var result = _loginValidator.Validate(new LoginRequest("jane@example.com", "Password1"));

        result.IsValid.Should().BeTrue();
    }
}
