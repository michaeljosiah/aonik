using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class GetConnectorPackageImpactEndpoint(IConnectorPackageInstallationService service) : EndpointWithoutRequest<ConnectorPackageImpact>
{
    public override void Configure() { Get("/v1/integrations/catalog/installations/{Id}/impact"); Policies("AdminUserPolicy"); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try { await Send.OkAsync(await service.PreviewAsync(Route<Guid>("Id"), Query<string>("targetVersion", isRequired: false), ct), ct); }
        catch (KeyNotFoundException) { await Send.NotFoundAsync(ct); }
        catch (InvalidOperationException error)
        {
            AddError(error.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
    }
}
