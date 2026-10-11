using System.Collections.Concurrent;
using System.Threading.Channels;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Queues;

public sealed class ChannelTaskQueue : ITaskQueue
{
    private const int Capacity = 1000;

    private readonly ConcurrentDictionary<string, Channel<Guid>> _channels = new();

    public async Task EnqueueAsync<T>(string queueName, T message, CancellationToken cancellationToken = default)
    {
        if (message is not Guid id)
        {
            throw new InvalidOperationException($"{nameof(ChannelTaskQueue)} only supports Guid messages.");
        }

        await GetChannel(queueName).Writer.WriteAsync(id, cancellationToken);
    }

    public ChannelReader<Guid> Reader(string queueName) => GetChannel(queueName).Reader;

    private Channel<Guid> GetChannel(string queueName) =>
        _channels.GetOrAdd(queueName, static _ => Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        }));
}
