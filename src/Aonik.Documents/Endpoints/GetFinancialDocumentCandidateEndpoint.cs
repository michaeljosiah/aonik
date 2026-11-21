using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Documents.Endpoints;

public sealed class GetFinancialDocumentCandidateEndpoint(IFinancialDocumentReader documents, ICurrentUserProvider currentUser)
    : EndpointWithoutRequest<FinancialDocumentCandidate>
{
    public override void Configure()
    {
        Get("/documents/financial-extractions/{id:guid}");
        Policies("AdminUserPolicy");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (currentUser.GetCurrentUserId() is not { } userId) { await Send.UnauthorizedAsync(ct); return; }
        try { await Send.OkAsync(await documents.RequireAsync(Route<Guid>("id"), new("User", userId, userId), ct), ct); }
        catch (PermissionDeniedException) { await Send.ForbiddenAsync(ct); }
        catch (NotFoundException) { await Send.NotFoundAsync(ct); }
        catch (ConnectorAccessException) { await Send.ForbiddenAsync(ct); }
        catch (FinancialDocumentException exception) { await Send.ResultAsync(Results.Conflict(new { code = exception.Code })); }
    }
}
