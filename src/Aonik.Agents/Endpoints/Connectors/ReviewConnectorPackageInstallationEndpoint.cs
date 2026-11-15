using FastEndpoints;
using Aonik.Agents.Endpoints.TenantExtensions;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class ReviewConnectorPackageInstallationEndpoint(IConnectorPackageInstallationService service) : TenantExtensionTransitionEndpoint<ReviewConnectorPackageInstallationRequest, ConnectorPackageInstallationItem>
{
    public override void Configure() { Post("/v1/integrations/catalog/installations/{Id}/review"); Policies("PlatformAdmin"); }
    public override Task HandleAsync(ReviewConnectorPackageInstallationRequest request, CancellationToken ct) => TransitionAsync(async () => await service.ReviewAsync(Route<Guid>("Id"), request, ct), ct);
}
