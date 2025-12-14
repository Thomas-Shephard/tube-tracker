namespace TubeTracker.API.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string title, string body);
}
