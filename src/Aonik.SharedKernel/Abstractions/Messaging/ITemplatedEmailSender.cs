namespace Aonik.SharedKernel.Abstractions.Messaging;

/// <summary>Renders a tenant-scoped email template and submits it to the configured sender.</summary>
public interface ITemplatedEmailSender
{
    Task SendAsync(TemplatedEmailMessage message, CancellationToken cancellationToken = default);
}
