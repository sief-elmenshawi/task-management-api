namespace TaskManagement.Application.Abstractions;

public interface ITaskProcessingQueue
{
    ValueTask EnqueueAsync(Guid taskId, CancellationToken cancellationToken = default);
}
