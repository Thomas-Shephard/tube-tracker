using MailKit.Net.Smtp;

namespace TubeTracker.API.Services;

public interface ISmtpClientFactory
{
    ISmtpClient Create();
}
