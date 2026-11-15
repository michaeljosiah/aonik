using FastEndpoints;
using Aonik.Agents.Endpoints.TenantExtensions;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class ImportConnectorPackageEndpoint(IConnectorPackageInstallationService service) : TenantExtensionTransitionEndpoint<ImportConnectorPackageRequest, IReadOnlyList<ConnectorPackageInstallationItem>>
{
    public override void Configure() { Post("/v1/integrations/catalog/packages/import"); Policies("AdminUserPolicy"); }
    public override Task HandleAsync(ImportConnectorPackageRequest request, CancellationToken ct) => TransitionAsync(async () => await service.ImportAsync(request, ct), ct);
}
