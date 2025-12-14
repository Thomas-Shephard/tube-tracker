using MailKit.Net.Smtp;

namespace TubeTracker.API.Services;

public class SmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClient Create() => new SmtpClient();
}
