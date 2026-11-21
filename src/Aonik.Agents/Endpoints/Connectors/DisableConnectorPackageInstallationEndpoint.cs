using FastEndpoints;
using Aonik.Agents.Endpoints.TenantExtensions;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class DisableConnectorPackageInstallationEndpoint(IConnectorPackageInstallationService service) : TenantExtensionTransitionEndpoint<ConnectorPackageInstallationCommand, ConnectorPackageInstallationItem>
{
    public override void Configure() { Post("/v1/integrations/catalog/installations/{Id}/disable"); Policies("AdminUserPolicy"); }
    public override Task HandleAsync(ConnectorPackageInstallationCommand request, CancellationToken ct) => TransitionAsync(async () => await service.DisableAsync(Route<Guid>("Id"), request, ct), ct);
}
