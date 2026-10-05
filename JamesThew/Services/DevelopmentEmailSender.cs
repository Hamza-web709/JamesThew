namespace JamesThew.Services;

public class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger) : IEmailSender
{
    public static readonly List<DevelopmentEmailMessage> SentMessages = [];

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        lock (SentMessages)
        {
            SentMessages.Add(new DevelopmentEmailMessage(toEmail, subject, htmlBody, DateTime.UtcNow));
        }
        logger.LogInformation("Development email test sink captured message to {Recipient}.", toEmail);
        return Task.CompletedTask;
    }
}

public sealed record DevelopmentEmailMessage(string ToEmail, string Subject, string HtmlBody, DateTime SentAtUtc);
