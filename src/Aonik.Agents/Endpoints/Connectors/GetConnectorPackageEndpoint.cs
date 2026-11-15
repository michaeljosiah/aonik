using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class GetConnectorPackageEndpoint(IConnectorPackageCatalog catalog) : EndpointWithoutRequest<ConnectorPackageDetail>
{
    public override void Configure() { Get("/integrations/catalog/packages/{packageKey}/{version}"); Policies("AdminUserPolicy"); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        ConnectorPackageDetail? package = await catalog.GetAsync(Route<string>("packageKey")!, Route<string>("version")!, ct);
        if (package is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(package, ct);
    }
}
