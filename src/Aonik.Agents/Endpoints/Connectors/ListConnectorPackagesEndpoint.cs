using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class ListConnectorPackagesEndpoint(IConnectorPackageCatalog catalog) : EndpointWithoutRequest<IReadOnlyList<ConnectorPackageSummary>>
{
    public override void Configure() { Get("/v1/integrations/catalog/packages"); Policies("AdminUserPolicy"); }
    public override async Task HandleAsync(CancellationToken ct) => await Send.OkAsync(await catalog.ListAsync(ct), ct);
}
