using FastEndpoints;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class ListConnectorPackageInstallationsEndpoint(IConnectorPackageInstallationService service) : EndpointWithoutRequest<IReadOnlyList<ConnectorPackageInstallationItem>>
{
    public override void Configure() { Get("/v1/integrations/catalog/installations"); Policies("AdminUserPolicy"); }
    public override async Task HandleAsync(CancellationToken ct) => await Send.OkAsync(await service.ListAsync(ct), ct);
}
