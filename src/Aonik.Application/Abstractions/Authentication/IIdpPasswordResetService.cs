namespace Aonik.Application.Abstractions.Authentication;

public interface IIdpPasswordResetService
{
    Task TriggerResetAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);
}
