namespace Aonik.Platform.Contracts.Services.Messaging;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
