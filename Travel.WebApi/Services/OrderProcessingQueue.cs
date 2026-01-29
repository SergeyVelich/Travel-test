using System.Threading.Channels;

namespace Travel.WebApi.Services;

public interface IOrderProcessingQueue
{
    ValueTask EnqueueAsync(int orderId, CancellationToken cancellationToken);
    ValueTask<int?> DequeueAsync(CancellationToken cancellationToken);
}

public class OrderProcessingQueue : IOrderProcessingQueue
{
    //TODO: use rabbitmq or other message broker for production scenarios
    private readonly Channel<int> _channel;

    public OrderProcessingQueue()
    {
        _channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(int orderId, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(orderId, cancellationToken);
    }

    public async ValueTask<int?> DequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var item = await _channel.Reader.ReadAsync(cancellationToken);
            return item;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
