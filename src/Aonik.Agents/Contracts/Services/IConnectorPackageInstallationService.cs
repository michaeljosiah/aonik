using Aonik.Agents.Contracts.Models.Tenant;

namespace Aonik.Agents.Contracts.Services;

public interface IConnectorPackageInstallationService
{
    Task<IReadOnlyList<ConnectorPackageInstallationItem>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConnectorPackageInstallationItem>> ImportAsync(ImportConnectorPackageRequest request, CancellationToken cancellationToken = default);
    Task<ConnectorPackageInstallationItem> SubmitAsync(Guid id, ConnectorPackageInstallationCommand command, CancellationToken cancellationToken = default);
    Task<ConnectorPackageInstallationItem> ReviewAsync(Guid id, ReviewConnectorPackageInstallationRequest request, CancellationToken cancellationToken = default);
    Task<ConnectorPackageInstallationItem> ActivateAsync(Guid id, ConnectorPackageInstallationCommand command, CancellationToken cancellationToken = default);
    Task<ConnectorPackageInstallationItem> DisableAsync(Guid id, ConnectorPackageInstallationCommand command, CancellationToken cancellationToken = default);
}
