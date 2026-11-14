using FastEndpoints;
using FluentValidation;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record ConnectorMutationStatusRequest(Guid ApprovalRequestId);
internal sealed class ConnectorMutationStatusValidator : Validator<ConnectorMutationStatusRequest>
{
    public ConnectorMutationStatusValidator() => RuleFor(request => request.ApprovalRequestId).NotEmpty();
}
internal sealed class GetConnectorMutationStatusEndpoint(IConnectorMutationService mutations) : Endpoint<ConnectorMutationStatusRequest>
{
    public override void Configure() => Get("/v1/integrations/mutations/{ApprovalRequestId:guid}");
    public override async Task HandleAsync(ConnectorMutationStatusRequest request, CancellationToken ct)
    {
        await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => mutations.GetStatusAsync(request.ApprovalRequestId, cancellationToken: ct)));
    }
}
internal sealed class GetConnectorMutationReviewEndpoint(IConnectorMutationService mutations) : Endpoint<ConnectorMutationStatusRequest>
{
    public override void Configure() => Get("/v1/integrations/mutations/{ApprovalRequestId:guid}/review");
    public override async Task HandleAsync(ConnectorMutationStatusRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => mutations.RequireReviewAsync(request.ApprovalRequestId, ct)));
}
internal sealed class ReconcileConnectorMutationEndpoint(IConnectorMutationService mutations) : Endpoint<ConnectorMutationStatusRequest>
{
    public override void Configure() => Post("/v1/integrations/mutations/{ApprovalRequestId:guid}/reconcile");
    public override async Task HandleAsync(ConnectorMutationStatusRequest request, CancellationToken ct)
    {
        await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => mutations.GetStatusAsync(request.ApprovalRequestId, reconcile: true, cancellationToken: ct)));
    }
}
