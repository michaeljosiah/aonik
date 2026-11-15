using FastEndpoints;
using Aonik.Agents.Endpoints.TenantExtensions;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class SubmitConnectorPackageInstallationEndpoint(IConnectorPackageInstallationService service) : TenantExtensionTransitionEndpoint<ConnectorPackageInstallationCommand, ConnectorPackageInstallationItem>
{
    public override void Configure() { Post("/v1/integrations/catalog/installations/{Id}/submit"); Policies("AdminUserPolicy"); }
    public override Task HandleAsync(ConnectorPackageInstallationCommand request, CancellationToken ct) => TransitionAsync(async () => await service.SubmitAsync(Route<Guid>("Id"), request, ct), ct);
}
