namespace TubeTracker.API.Services.Background;

public class EmailBackgroundService(IEmailQueue taskQueue, IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EmailMessage message = await taskQueue.DequeueAsync(stoppingToken);

                using IServiceScope scope = serviceScopeFactory.CreateScope();
                IEmailService emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                await emailService.SendEmailAsync(message.To, message.Subject, message.Title, message.Body);
            }
            catch (OperationCanceledException)
            {
                // Execution cancelled
            }
            catch (Exception)
            {
                // Ignored
            }
        }
    }
}
