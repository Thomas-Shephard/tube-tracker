namespace TubeTracker.API.Services;

public class MockEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string title, string body)
    {
        Console.WriteLine($"MockEmailService: Sending email to {to} with subject {subject}");
        Console.WriteLine($"Title: {title}");
        Console.WriteLine($"Body: {body}");
        return Task.CompletedTask;
    }
}
