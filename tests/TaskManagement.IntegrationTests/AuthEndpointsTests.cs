using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace TaskManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthEndpointsTests
{
    private readonly ApiFixture _fixture;

    public AuthEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    [IntegrationFact]
    public async Task Me_WithoutToken_Returns401WithBearerChallenge()
    {
        var client = await _fixture.CreateClientAsync();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle()
            .Which.Scheme.Should().Be("Bearer");
    }

    [IntegrationFact]
    public async Task Me_WithInvalidToken_Returns401WithInvalidTokenChallenge()
    {
        var client = await _fixture.CreateClientAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle()
            .Which.Parameter.Should().Contain("invalid_token");
    }

    [IntegrationFact]
    public async Task Register_Login_And_Me_ReturnsTheCurrentUser()
    {
        var client = await _fixture.CreateClientAsync();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterBody("Jane", email, "Password1", "Password1"));
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginBody(email, "Password1"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseBody>();
        tokens.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var me = await client.GetAsync("/api/auth/me");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("email").GetString().Should().Be(email);
    }

    [IntegrationFact]
    public async Task Register_WithDuplicateEmail_Returns409EmailAlreadyExists()
    {
        var client = await _fixture.CreateClientAsync();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterBody("Jane", email, "Password1", "Password1"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterBody("Jane", email, "Password1", "Password1"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("EmailAlreadyExists");
    }

    [IntegrationFact]
    public async Task Refresh_ReusingARotatedToken_RevokesEverySession()
    {
        var client = await _fixture.CreateClientAsync();
        var email = $"reuse-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterBody("Jane", email, "Password1", "Password1"));

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginBody(email, "Password1"));
        var first = await login.Content.ReadFromJsonAsync<AuthResponseBody>();

        var rotate = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshBody(first!.RefreshToken));
        rotate.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await rotate.Content.ReadFromJsonAsync<AuthResponseBody>();
        rotated!.RefreshToken.Should().NotBe(first.RefreshToken);

        // The grace window is 0 in the test host, so presenting the rotated token again is theft.
        await Task.Delay(30);
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshBody(first.RefreshToken));
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using (var body = JsonDocument.Parse(await reuse.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("code").GetString().Should().Be("RefreshTokenReuseDetected");
        }

        // Reuse detection revoked every active session, including the freshly rotated token.
        var afterRevoke = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshBody(rotated.RefreshToken));
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
