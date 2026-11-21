using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Documents.Endpoints;

public sealed class GetFinancialDocumentContentEndpoint(IFinancialDocumentReader documents, ICurrentUserProvider currentUser)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/documents/{documentId:guid}/files/{fileId:guid}/financial-content");
        Policies("AdminUserPolicy");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (currentUser.GetCurrentUserId() is not { } userId) { await Send.UnauthorizedAsync(ct); return; }
        try
        {
            FinancialDocumentFileContent result = await documents.OpenFileAsync(Route<Guid>("documentId"), Route<Guid>("fileId"), new("User", userId, userId), ct);
            await using Stream stream = result.Content;
            HttpContext.Response.Headers.CacheControl = "no-store";
            HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
            await Send.StreamAsync(stream, result.FileName, contentType: result.ContentType, enableRangeProcessing: false, cancellation: ct);
        }
        catch (PermissionDeniedException) { await Send.ForbiddenAsync(ct); }
        catch (NotFoundException) { await Send.NotFoundAsync(ct); }
        catch (ConnectorAccessException) { await Send.ForbiddenAsync(ct); }
        catch (FinancialDocumentException exception) { await Send.ResultAsync(Results.Conflict(new { code = exception.Code })); }
    }
}
