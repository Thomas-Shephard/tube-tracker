namespace TubeTracker.API.Services;

public class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string title, string body)
    {
        logger.LogInformation("MockEmailService: Sending email to {To} with subject {Subject}", to, subject);
        logger.LogInformation("Title: {Title}", title);
        logger.LogInformation("Body: {Body}", body);

        return Task.CompletedTask;
    }
}
