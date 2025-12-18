namespace TubeTracker.API.Services.Background;

public class EmailBackgroundService(IEmailQueue taskQueue, IServiceScopeFactory serviceScopeFactory, ILogger<EmailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EmailMessage message = await taskQueue.DequeueAsync(stoppingToken);
                logger.LogDebug("Processing background email to {To}", message.To);

                using IServiceScope scope = serviceScopeFactory.CreateScope();
                IEmailService emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                await emailService.SendEmailAsync(message.To, message.Subject, message.Title, message.Body);
            }
            catch (OperationCanceledException)
            {
                // Execution cancelled
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while sending background email.");
            }
        }
    }
}
