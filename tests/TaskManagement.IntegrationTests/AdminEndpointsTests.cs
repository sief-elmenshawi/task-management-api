using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace TaskManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AdminEndpointsTests
{
    private const string AdminEmail = "admin@example.com";
    private const string AdminPassword = "Admin@123";

    private readonly ApiFixture _fixture;

    public AdminEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    [IntegrationFact]
    public async Task GetAllUsers_AsAdmin_Returns200WithTheSeededAdmin()
    {
        var client = await CreateAdminClientAsync();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        users.Should().Contain(u => u.GetProperty("email").GetString() == AdminEmail);
    }

    [IntegrationFact]
    public async Task GetAllUsers_AsRegularUser_Returns403()
    {
        var client = await CreateRegularClientAsync();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [IntegrationFact]
    public async Task GetAllUsers_WithoutToken_Returns401()
    {
        var client = await _fixture.CreateClientAsync();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [IntegrationFact]
    public async Task CreateUser_AsAdmin_Returns201WithRegularRole()
    {
        var client = await CreateAdminClientAsync();
        var email = $"created-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new RegisterBody("Created", email, "Password1", "Password1"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("email").GetString().Should().Be(email);
        body.RootElement.GetProperty("role").GetString().Should().Be("User");
    }

    [IntegrationFact]
    public async Task CreateUser_AsRegularUser_Returns403()
    {
        var client = await CreateRegularClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new RegisterBody("Forbidden", $"forbidden-{Guid.NewGuid():N}@example.com", "Password1", "Password1"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [IntegrationFact]
    public async Task DeleteUser_AsAdmin_Returns204AndTheAccountCanNoLongerLogIn()
    {
        var admin = await CreateAdminClientAsync();
        var email = $"deletable-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAsync(admin, email);

        var deleted = await admin.DeleteAsync($"/api/users/{id}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await admin.PostAsJsonAsync("/api/auth/login", new LoginBody(email, "Password1"));
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var users = await admin.GetFromJsonAsync<List<JsonElement>>("/api/users");
        users.Should().NotContain(u => u.GetProperty("email").GetString() == email);
    }

    [IntegrationFact]
    public async Task DeleteUser_AsRegularUser_Returns403()
    {
        var admin = await CreateAdminClientAsync();
        var email = $"victim-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAsync(admin, email);

        var regular = await CreateRegularClientAsync();
        var response = await regular.DeleteAsync($"/api/users/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [IntegrationFact]
    public async Task DeleteUser_WithoutToken_Returns401()
    {
        var client = await _fixture.CreateClientAsync();

        var response = await client.DeleteAsync($"/api/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [IntegrationFact]
    public async Task DeleteOwnAccount_AsAdmin_Returns403CannotDeleteSelf()
    {
        var admin = await CreateAdminClientAsync();
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var id = me.GetProperty("id").GetGuid();

        var response = await admin.DeleteAsync($"/api/users/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("CannotDeleteSelf");
    }

    private static async Task<Guid> CreateUserAsync(HttpClient admin, string email)
    {
        var response = await admin.PostAsJsonAsync(
            "/api/users",
            new RegisterBody("Managed", email, "Password1", "Password1"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = await _fixture.CreateClientAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginBody(AdminEmail, AdminPassword));
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateRegularClientAsync()
    {
        var client = await _fixture.CreateClientAsync();
        var email = $"regular-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterBody("Regular", email, "Password1", "Password1"));
        register.IsSuccessStatusCode.Should().BeTrue();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginBody(email, "Password1"));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }
}
