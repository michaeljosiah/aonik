namespace Aonik.Platform.Contracts.Services.Messaging;

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}
