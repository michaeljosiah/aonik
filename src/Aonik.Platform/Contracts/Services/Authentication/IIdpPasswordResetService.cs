namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IIdpPasswordResetService
{
    Task TriggerResetAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);
}
