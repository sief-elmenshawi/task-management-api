namespace TaskManagement.Application.Abstractions;

public interface ITaskQueue
{
    Task EnqueueAsync<T>(string queueName, T message, CancellationToken cancellationToken = default);
}
