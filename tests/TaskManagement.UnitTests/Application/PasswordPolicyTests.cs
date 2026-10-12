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
        var password = new string('é', 36) + "a1";

        var result = _registerValidator.Validate(
            new RegisterRequest("Jane", "jane@example.com", password, password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.ErrorMessage.Contains("72 bytes (UTF-8)", StringComparison.Ordinal));
    }

    [Fact]
    public void Register_AcceptsPasswordExactlyAtThe72Utf8ByteLimit()
    {
        var password = new string('é', 35) + "a1";

        var result = _registerValidator.Validate(
            new RegisterRequest("Jane", "jane@example.com", password, password));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Register_AcceptsPlainAsciiPassword()
    {
        var result = _registerValidator.Validate(
            new RegisterRequest("Jane", "jane@example.com", "Password1", "Password1"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Login_RejectsPasswordExceeding72Utf8Bytes()
    {
        var password = new string('é', 36) + "a1";

        var result = _loginValidator.Validate(new LoginRequest("jane@example.com", password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.ErrorMessage.Contains("72 bytes (UTF-8)", StringComparison.Ordinal));
    }

    [Fact]
    public void Login_AcceptsPasswordExactlyAtThe72Utf8ByteLimit()
    {
        var password = new string('é', 35) + "a1";

        var result = _loginValidator.Validate(new LoginRequest("jane@example.com", password));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Register_PartialRequestWithMissingPassword_DoesNotThrow()
    {
        var result = _registerValidator.Validate(
            new RegisterRequest(null!, "jane@example.com", null!, null!));

        result.Errors.Should().Contain(error =>
            error.ErrorMessage == "Password is required.");
    }

    [Fact]
    public void Login_PartialRequestWithMissingPassword_DoesNotThrow()
    {
        var result = _loginValidator.Validate(new LoginRequest("jane@example.com", null!));

        result.Errors.Should().Contain(error =>
            error.ErrorMessage == "Password is required.");
    }
}