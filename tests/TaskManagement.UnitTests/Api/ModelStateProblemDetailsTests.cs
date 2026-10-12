using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskManagement.Api.Extensions;

namespace TaskManagement.UnitTests.Api;

public class ModelStateProblemDetailsTests
{
    [Fact]
    public void Create_ReturnsRequestBodyRequired_WhenBodyKeyIsEmpty()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(string.Empty, "A non-empty request body is required.");

        var errors = Errors(ModelStateProblemDetails.Create(modelState));

        errors.Should().ContainKey("request");
        errors["request"].Should().ContainSingle()
            .Which.Should().Be("A request body is required.");
    }

    [Fact]
    public void Create_ReturnsRequestBodyRequired_WhenBodyCannotBeParsed()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$", "The JSON value could not be converted to RegisterRequest.");

        var errors = Errors(ModelStateProblemDetails.Create(modelState));

        errors.Should().ContainKey("request");
    }

    [Fact]
    public void Create_MapsJsonPathKey_ToCamelCaseField()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.title", "The JSON value could not be converted to System.String.");

        var errors = Errors(ModelStateProblemDetails.Create(modelState));

        errors.Should().ContainKey("title");
        errors["title"].Should().ContainSingle()
            .Which.Should().Be("The value of 'title' is invalid.");
    }

    [Fact]
    public void Create_MapsPlainPropertyKey_ToCamelCaseField()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The JSON value could not be converted to System.String.");

        var errors = Errors(ModelStateProblemDetails.Create(modelState));

        errors.Should().ContainKey("email");
        errors["email"].Should().ContainSingle()
            .Which.Should().Be("The value of 'email' is invalid.");
    }

    [Theory]
    [InlineData("$.priority", "priority", "Invalid value. Allowed values: Low, Medium, High.")]
    [InlineData("$.status", "status", "Invalid value. Allowed values: Pending, InProgress, Done.")]
    [InlineData("id", "id", "The id must be a valid GUID.")]
    [InlineData("$.id", "id", "The id must be a valid GUID.")]
    public void Create_KeepsReadableMessages_ForEnumAndGuid(string key, string field, string message)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(key, "raw framework message");

        var errors = Errors(ModelStateProblemDetails.Create(modelState));

        errors.Should().ContainKey(field);
        errors[field].Should().ContainSingle().Which.Should().Be(message);
    }

    [Fact]
    public void Create_ReturnsRequestBodyRequired_WhenModelStateHasNoMessages()
    {
        var errors = Errors(ModelStateProblemDetails.Create(new ModelStateDictionary()));

        errors.Should().ContainKey("request");
    }

    private static Dictionary<string, string[]> Errors(ProblemDetails problemDetails) =>
        (Dictionary<string, string[]>)problemDetails.Extensions["errors"]!;
}
