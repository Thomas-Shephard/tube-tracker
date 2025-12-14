namespace TubeTracker.API.Services.Background;

public record EmailMessage(string To, string Subject, string Title, string Body);

public interface IEmailQueue
{
    ValueTask QueueBackgroundEmailAsync(EmailMessage message);
    ValueTask<EmailMessage> DequeueAsync(CancellationToken cancellationToken);
}
