namespace JamesThew.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public class EmailSendException(string message, Exception? innerException = null) : Exception(message, innerException);
