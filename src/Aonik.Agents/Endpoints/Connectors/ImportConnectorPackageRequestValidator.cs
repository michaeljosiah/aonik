using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Models.Tenant;

namespace Aonik.Agents.Endpoints.Connectors;

public sealed class ImportConnectorPackageRequestValidator : Validator<ImportConnectorPackageRequest>
{
    public ImportConnectorPackageRequestValidator()
    {
        RuleFor(request => request.PackageKey).Matches("^[a-z][a-z0-9_.-]{0,99}$").NotEmpty();
        RuleFor(request => request.PackageVersion).Matches("^[0-9]{1,10}\\.[0-9]{1,10}\\.[0-9]{1,10}$").NotEmpty();
        RuleFor(request => request.Digest).Matches("^[0-9a-f]{64}$").NotEmpty();
        RuleFor(request => request.AuthSecret).MaximumLength(16_384).Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("The credential contains invalid characters.");
        RuleFor(request => request.AuthUsername).MaximumLength(500).Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("The credential username contains invalid characters.");
    }
}
