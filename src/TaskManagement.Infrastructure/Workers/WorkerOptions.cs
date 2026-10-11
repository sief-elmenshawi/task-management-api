namespace TaskManagement.Infrastructure.Workers;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public int Concurrency { get; set; } = 3;
}
