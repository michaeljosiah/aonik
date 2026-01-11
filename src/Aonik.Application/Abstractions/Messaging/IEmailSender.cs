namespace Aonik.Application.Abstractions.Messaging;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
