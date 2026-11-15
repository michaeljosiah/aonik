using FluentValidation;
using FastEndpoints;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Endpoints.TenantExtensions;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class RotateConnectorPackageCredentialEndpoint(IConnectorPackageInstallationService service)
    : TenantExtensionTransitionEndpoint<RotateConnectorPackageCredentialRequest, ConnectorPackageInstallationItem>
{
    public override void Configure() { Post("/v1/integrations/catalog/installations/{Id}/credential"); Policies("AdminUserPolicy"); }
    public override Task HandleAsync(RotateConnectorPackageCredentialRequest request, CancellationToken ct)
        => TransitionAsync(async () => await service.RotateCredentialAsync(Route<Guid>("Id"), request, ct), ct);
}

public sealed class RotateConnectorPackageCredentialRequestValidator : Validator<RotateConnectorPackageCredentialRequest>
{
    public RotateConnectorPackageCredentialRequestValidator()
    {
        RuleFor(request => request.ExpectedVersion).NotNull().MaximumLength(32);
        RuleFor(request => request.AuthSecret).NotEmpty().MaximumLength(16_384);
        RuleFor(request => request.AuthUsername).MaximumLength(500);
    }
}
