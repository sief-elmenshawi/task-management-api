using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace TaskManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public class WorkerAndCacheTests
{
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromSeconds(40);

    private readonly ApiFixture _fixture;

    public WorkerAndCacheTests(ApiFixture fixture) => _fixture = fixture;

    [IntegrationFact]
    public async Task Worker_MovesPendingTaskToInProgress_AndPersistsTheChange()
    {
        var client = await CreateAuthenticatedClientAsync();
        var task = await CreateTaskAsync(client);

        task.Status.Should().Be("Pending");

        var status = await WaitForStatusAsync(client, task.Id, "InProgress");

        status.Should().Be("InProgress");

        // The list endpoint reads straight from SQL Server, so the transition is durable.
        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/tasks");
        Find(list!, task.Id).GetProperty("status").GetString().Should().Be("InProgress");
    }

    [IntegrationFact]
    public async Task TaskById_DoesNotServeTheStalePendingSnapshotAfterTheWorkerEvictsIt()
    {
        var client = await CreateAuthenticatedClientAsync();
        var task = await CreateTaskAsync(client);

        // The first read populates the Redis cache with the Pending snapshot.
        var initial = await client.GetFromJsonAsync<JsonElement>($"/api/tasks/{task.Id}");
        initial.GetProperty("status").GetString().Should().Be("Pending");

        await WaitForStatusAsync(client, task.Id, "InProgress");

        // The worker committed InProgress and evicted the key, so the cache must not leak Pending.
        var afterWorker = await client.GetFromJsonAsync<JsonElement>($"/api/tasks/{task.Id}");
        afterWorker.GetProperty("status").GetString().Should().Be("InProgress");
    }

    [IntegrationFact]
    public async Task TaskById_ReflectsAStatusChangeWithoutServingAStaleCacheEntry()
    {
        var client = await CreateAuthenticatedClientAsync();
        var task = await CreateTaskAsync(client);

        // Warm the cache with whatever the worker left behind.
        var initial = await client.GetFromJsonAsync<JsonElement>($"/api/tasks/{task.Id}");
        initial.GetProperty("status").GetString().Should().BeOneOf("Pending", "InProgress");

        var patch = await client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new UpdateTaskStatusBody("Done"));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterPatch = await client.GetFromJsonAsync<JsonElement>($"/api/tasks/{task.Id}");
        afterPatch.GetProperty("status").GetString().Should().Be("Done");
    }

    private static async Task<string> WaitForStatusAsync(HttpClient client, Guid taskId, string expected)
    {
        var deadline = DateTime.UtcNow + WorkerTimeout;
        string? observed = null;

        while (DateTime.UtcNow < deadline)
        {
            var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/tasks");
            observed = Find(list!, taskId).GetProperty("status").GetString();

            if (observed == expected)
            {
                return observed;
            }

            await Task.Delay(250);
        }

        observed.Should().Be(expected, "the background worker should transition the task within the timeout");
        return observed!;
    }

    private static JsonElement Find(List<JsonElement> tasks, Guid taskId)
    {
        foreach (var task in tasks)
        {
            if (task.GetProperty("id").GetGuid() == taskId)
            {
                return task;
            }
        }

        return default;
    }

    private static async Task<TaskSnapshot> CreateTaskAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskBody($"Worker {Guid.NewGuid():N}", "created by the worker test", "High"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new TaskSnapshot(
            body.RootElement.GetProperty("id").GetGuid(),
            body.RootElement.GetProperty("status").GetString()!);
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

    private sealed record TaskSnapshot(Guid Id, string Status);
}
