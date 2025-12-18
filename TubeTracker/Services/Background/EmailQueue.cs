using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace TubeTracker.API.Services.Background;

public class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _queue;
    private readonly ILogger<EmailQueue> _logger;

    public EmailQueue(ILogger<EmailQueue> logger)
    {
        _logger = logger;
        UnboundedChannelOptions options = new()
        {
            SingleReader = true,
            SingleWriter = false
        };
        _queue = Channel.CreateUnbounded<EmailMessage>(options);
    }

    public ValueTask QueueBackgroundEmailAsync(EmailMessage message)
    {
        _logger.LogInformation("Queuing email to {To} with subject: {Subject}", message.To, message.Subject);
        return _queue.Writer.WriteAsync(message);
    }

    public async ValueTask<EmailMessage> DequeueAsync(CancellationToken cancellationToken)
    {
        EmailMessage message = await _queue.Reader.ReadAsync(cancellationToken);
        _logger.LogDebug("Dequeued email to {To} with subject: {Subject}", message.To, message.Subject);
        return message;
    }
}
