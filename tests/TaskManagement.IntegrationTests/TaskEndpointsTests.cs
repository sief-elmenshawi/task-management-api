using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace TaskManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TaskEndpointsTests
{
    private readonly ApiFixture _fixture;

    public TaskEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    [IntegrationFact]
    public async Task CreateTask_ThenDuplicateTitleSameDay_Returns409()
    {
        var client = await CreateAuthenticatedClientAsync();
        var title = $"Task {Guid.NewGuid():N}";

        var create = await client.PostAsJsonAsync("/api/tasks", new CreateTaskBody(title, "first", "High"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.Location.Should().NotBeNull();

        var duplicate = await client.PostAsJsonAsync("/api/tasks", new CreateTaskBody(title, "second", "Low"));

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("TaskTitleDuplicate");
    }

    [IntegrationFact]
    public async Task ListTasks_IsOwnerScoped()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var other = await CreateAuthenticatedClientAsync();

        await owner.PostAsJsonAsync("/api/tasks", new CreateTaskBody($"Only {Guid.NewGuid():N}", null, "Medium"));

        var ownerList = await owner.GetFromJsonAsync<List<JsonElement>>("/api/tasks");
        var otherList = await other.GetFromJsonAsync<List<JsonElement>>("/api/tasks");

        ownerList.Should().NotBeNullOrEmpty();
        otherList.Should().BeEmpty();
    }

    [IntegrationFact]
    public async Task MalformedJsonBody_Returns400ValidationProblem()
    {
        var client = await CreateAuthenticatedClientAsync();
        using var content = new StringContent("{ \"title\": ", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/tasks", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("ValidationFailed");
        body.RootElement.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = await _fixture.CreateClientAsync();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterBody("Jane", email, "Password1", "Password1"));
        register.IsSuccessStatusCode.Should().BeTrue();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginBody(email, "Password1"));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseBody>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }
}
