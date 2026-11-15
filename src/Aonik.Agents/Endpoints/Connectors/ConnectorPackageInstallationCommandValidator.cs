using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Models.Tenant;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class ConnectorPackageInstallationCommandValidator : Validator<ConnectorPackageInstallationCommand>
{
    public ConnectorPackageInstallationCommandValidator()
    {
        RuleFor(request => request.ExpectedVersion).NotNull().MaximumLength(32);
        RuleFor(request => request.Notes).MaximumLength(2000);
    }
}
