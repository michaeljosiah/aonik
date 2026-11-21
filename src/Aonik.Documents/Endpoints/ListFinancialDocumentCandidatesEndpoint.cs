using FastEndpoints;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Documents.Endpoints;

public sealed class ListFinancialDocumentCandidatesEndpoint(IFinancialDocumentReader documents, ICurrentUserProvider currentUser)
    : EndpointWithoutRequest<IReadOnlyList<FinancialDocumentExtractionReceipt>>
{
    public override void Configure()
    {
        Get("/documents/{documentId:guid}/financial-extractions");
        Policies("AdminUserPolicy");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (currentUser.GetCurrentUserId() is not { } userId) { await Send.UnauthorizedAsync(ct); return; }
        try { await Send.OkAsync(await documents.ListAsync(Route<Guid>("documentId"), new("User", userId, userId), ct), ct); }
        catch (PermissionDeniedException) { await Send.ForbiddenAsync(ct); }
        catch (NotFoundException) { await Send.NotFoundAsync(ct); }
        catch (ConnectorAccessException) { await Send.ForbiddenAsync(ct); }
    }
}
