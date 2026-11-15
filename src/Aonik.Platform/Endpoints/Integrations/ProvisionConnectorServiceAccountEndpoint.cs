using FastEndpoints;
using FluentValidation;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

public sealed record ProvisionConnectorServiceAccountRequest(string AdapterKind, Guid InstallationId, string DisplayName);

public sealed class ProvisionConnectorServiceAccountEndpoint(IConnectorService connectors) : Endpoint<ProvisionConnectorServiceAccountRequest>
{
    public override void Configure() => Post("/v1/integrations/connectors/{connectorKey}/service-accounts");

    public override async Task HandleAsync(ProvisionConnectorServiceAccountRequest request, CancellationToken ct)
    {
        string accountId = $"{request.AdapterKind.ToLowerInvariant()}:{request.InstallationId:N}";
        await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => connectors.ProvisionServiceAccountAsync(Route<string>("connectorKey")!,
            accountId, request.DisplayName, ct)));
    }
}

public sealed class ProvisionConnectorServiceAccountRequestValidator : Validator<ProvisionConnectorServiceAccountRequest>
{
    public ProvisionConnectorServiceAccountRequestValidator()
    {
        RuleFor(request => request.AdapterKind).Must(kind => kind is "Mcp" or "Http");
        RuleFor(request => request.InstallationId).NotEmpty();
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(160);
    }
}
