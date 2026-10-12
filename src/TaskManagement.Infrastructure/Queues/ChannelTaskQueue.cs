using System.Threading.Channels;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Queues;

public sealed class ChannelTaskQueue : ITaskProcessingQueue
{
    private const int Capacity = 1000;

    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacity)
    {
        SingleReader = false,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
    });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(taskId, cancellationToken);
}
