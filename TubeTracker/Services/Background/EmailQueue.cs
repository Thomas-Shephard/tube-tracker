using System.Threading.Channels;

namespace TubeTracker.API.Services.Background;

public class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _queue;

    public EmailQueue()
    {
        UnboundedChannelOptions options = new()
        {
            SingleReader = true,
            SingleWriter = false
        };
        _queue = Channel.CreateUnbounded<EmailMessage>(options);
    }

    public ValueTask QueueBackgroundEmailAsync(EmailMessage message)
    {
        return _queue.Writer.WriteAsync(message);
    }

    public ValueTask<EmailMessage> DequeueAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}
