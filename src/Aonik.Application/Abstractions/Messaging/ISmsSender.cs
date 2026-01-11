namespace Aonik.Application.Abstractions.Messaging;

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}
