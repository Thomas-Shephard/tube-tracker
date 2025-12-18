using System.ComponentModel.DataAnnotations;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services;

public class SmtpEmailService : IEmailService
{
    private const string TemplateDirectory = "Templates";
    private const string TemplateFileName = "EmailTemplate.html";

    private readonly EmailSettings _settings;
    private readonly ISmtpClientFactory _clientFactory;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly string _template;

    public SmtpEmailService(EmailSettings settings, ISmtpClientFactory clientFactory, ILogger<SmtpEmailService> logger)
    {
        _settings = settings;
        _clientFactory = clientFactory;
        _logger = logger;

        string templatePath = Path.Combine(AppContext.BaseDirectory, TemplateDirectory, TemplateFileName);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Email template not found at {templatePath}");
        }

        _template = File.ReadAllText(templatePath);
    }

    public async Task SendEmailAsync(string to, string subject, string title, string body)
    {
        if (!new EmailAddressAttribute().IsValid(to))
        {
            throw new ArgumentException("Email address is not valid", nameof(to));
        }

        using MimeMessage message = new();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;

        string htmlBody = _template
                          .Replace("{{Title}}", WebUtility.HtmlEncode(title))
                          .Replace("{{Body}}", WebUtility.HtmlEncode(body));

        message.Body = new TextPart("html")
        {
            Text = htmlBody
        };

        using ISmtpClient client = _clientFactory.Create();

        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_settings.User, _settings.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Email sent successfully to {To} with subject {Subject}", to, subject);
    }
}
