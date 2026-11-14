using FastEndpoints;
using FluentValidation;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class CreateConnectionRequestValidator : Validator<CreateConnectionRequest>
{
    public CreateConnectionRequestValidator()
    {
        RuleFor(input => input.ConnectorKey).NotEmpty().Matches("^[a-z][a-z0-9-]{0,79}$");
        RuleFor(input => input.Ownership).Must(value => value is "User" or "Tenant");
        RuleFor(input => input.Purpose).NotEmpty().MaximumLength(1000);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128).Matches("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$");
        RuleFor(input => input.Actions).NotNull().Must(actions => actions is not null && actions.Count <= 100 && actions.All(action => action is not null
            && !string.IsNullOrWhiteSpace(action.Key) && action.Key.Length <= 100 && action.Version > 0));
        RuleFor(input => input.DesiredCapability).MaximumLength(500);
    }
}
internal sealed class ConnectionRequestCommandValidator : Validator<ConnectionRequestCommand>
{
    public ConnectionRequestCommandValidator() => RuleFor(input => input.ExpectedVersion).NotNull().MaximumLength(128);
}
internal sealed class DecideConnectionRequestValidator : Validator<DecideConnectionRequest>
{
    public DecideConnectionRequestValidator()
    {
        RuleFor(input => input.ExpectedVersion).NotNull().MaximumLength(128);
        RuleFor(input => input.Decision).Must(value => value is "Approve" or "Reject");
    }
}
internal sealed class ConnectorGrantCommandValidator : Validator<ConnectorGrantCommand>
{
    public ConnectorGrantCommandValidator()
    {
        RuleFor(input => input.GranteeType).Must(value => value is "User" or "Role" or "Workflow");
        RuleFor(input => input.GranteeId).NotEmpty();
        RuleFor(input => input.ExpectedConnectionVersion).NotNull().MaximumLength(128);
        RuleFor(input => input.ExpectedGrantVersion).MaximumLength(128);
        RuleFor(input => input.Actions).NotNull().Must(actions => actions is { Count: > 0 and <= 100 } && actions.All(action => action is not null
            && !string.IsNullOrWhiteSpace(action.Key) && action.Key.Length <= 100 && action.Version > 0));
    }
}
internal sealed class ConnectorReadinessHttpRequestValidator : Validator<ConnectorReadinessHttpRequest>
{
    public ConnectorReadinessHttpRequestValidator()
    {
        RuleFor(input => input.ConnectorKey).NotEmpty().MaximumLength(80);
        RuleFor(input => input.ActionKey).NotEmpty().MaximumLength(100);
        RuleFor(input => input.Version).GreaterThan(0);
    }
}
internal sealed class ListConnectionRequestsHttpRequestValidator : Validator<ListConnectionRequestsHttpRequest>
{
    public ListConnectionRequestsHttpRequestValidator()
    {
        RuleFor(input => input.Skip).InclusiveBetween(0, 10000);
        RuleFor(input => input.Take).InclusiveBetween(1, 100);
    }
}
internal sealed class ConnectorCallbackHttpRequestValidator : Validator<ConnectorCallbackHttpRequest>
{
    public ConnectorCallbackHttpRequestValidator()
    {
        RuleFor(input => input.State).MaximumLength(256);
        RuleFor(input => input.Code).MaximumLength(4096);
        RuleFor(input => input.Error).MaximumLength(100);
    }
}
internal sealed class RenameConnectionHttpRequestValidator : Validator<RenameConnectionHttpRequest>
{
    public RenameConnectionHttpRequestValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(input => input.ExpectedVersion).NotNull().MaximumLength(128);
    }
}
internal sealed class OpenConnectorContentHttpRequestValidator : Validator<OpenConnectorContentHttpRequest>
{
    public OpenConnectorContentHttpRequestValidator() => RuleFor(input => input.FileReference).NotEmpty().MaximumLength(8192);
}
