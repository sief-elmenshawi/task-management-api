namespace TaskManagement.IntegrationTests;

/// <summary>
/// A <see cref="FactAttribute"/> whose tests only run when the environment variable
/// <c>TASKMANAGER_INTEGRATION</c> is set to <c>1</c>. The integration tests start real SQL Server and
/// Redis containers, so they are opt-in and the default <c>dotnet test</c> run stays dependency-free.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TASKMANAGER_INTEGRATION") != "1")
        {
            Skip = "Set TASKMANAGER_INTEGRATION=1 (with Docker running) to execute the integration tests.";
        }
    }
}
