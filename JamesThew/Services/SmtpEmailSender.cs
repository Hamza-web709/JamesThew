using System.Net;
using System.Net.Mail;

namespace JamesThew.Services;

public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new EmailSendException("Email delivery is not configured. Set Email:Smtp:Host and Email:Smtp:From.");
        }

        var senderAddress = CreateSenderAddress(configuration);
        var port = configuration.GetValue("Email:Smtp:Port", 587);
        using var message = new MailMessage
        {
            From = senderAddress,
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Email:Smtp:EnableSsl", true)
        };

        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        try
        {
            using var registration = cancellationToken.Register(client.SendAsyncCancel);
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Email delivery failed for {Recipient}.", toEmail);
            throw new EmailSendException("Email delivery failed. Check SMTP connectivity and try again.", exception);
        }
    }

    public static MailAddress CreateSenderAddress(IConfiguration configuration)
    {
        var from = configuration["Email:Smtp:From"];
        if (string.IsNullOrWhiteSpace(from))
        {
            throw new EmailSendException("Email delivery is not configured. Set Email:Smtp:Host and Email:Smtp:From.");
        }

        var fromName = configuration["Email:Smtp:FromName"];
        if (string.IsNullOrWhiteSpace(fromName))
        {
            fromName = "JamesThew Sanctuary";
        }

        return new MailAddress(from, fromName);
    }
}
