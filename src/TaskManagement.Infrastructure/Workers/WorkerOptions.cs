namespace TaskManagement.Infrastructure.Workers;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public int Concurrency { get; set; } = 3;
    public int RecoveryIntervalSeconds { get; set; } = 60;
    public int RecoveryMinAgeSeconds { get; set; } = 30;
}
