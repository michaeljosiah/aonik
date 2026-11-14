using Microsoft.EntityFrameworkCore;

using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Documents.Services;

internal sealed class FinancialDocumentAuthorization(DocumentsDbContext database, ITenantProvider tenantProvider,
    IPermissionService permissions, IUserDirectoryReader users, IAutomationIdentityAuthorization? automation = null,
    IFinancialDocumentSourceAccess? sources = null)
{
    public async Task RequireExtractionAsync(Guid documentId, WorkflowActor actor, CancellationToken cancellationToken)
    {
        if (actor.Type == "AutomationIdentity" && automation is not null)
            await automation.RequireAsync(actor, "Document.Extract", "Document", documentId,
                new(FinancialDocumentActions.Extract, 1), cancellationToken);
        else if (actor.Type != "User" || !await permissions.HasPermissionAsync(actor.Id, "Document.Extract", cancellationToken))
            throw new PermissionDeniedException("Document.Extract");
        await RequireAsync(documentId, actor, cancellationToken);
    }

    public async Task RequireReaderAsync(WorkflowActor actor, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        if (actor.Type == "AutomationIdentity" && automation is not null)
            await automation.RequireAsync(actor, "Document.Read", cancellationToken: cancellationToken);
        else if (actor.Type != "User" || !(await users.GetByIdsAsync(tenantId, [actor.Id], cancellationToken))
            .Any(user => user.UserId == actor.Id && user.Status == "Active")
            || !await permissions.HasPermissionAsync(actor.Id, "Document.Read", cancellationToken))
            throw new PermissionDeniedException("Document.Read");
    }

    public async Task<Document> RequireAsync(Guid documentId, WorkflowActor actor, CancellationToken cancellationToken = default)
    {
        await RequireReaderAsync(actor, cancellationToken);
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        if (actor.Type == "AutomationIdentity") await automation!.RequireAsync(actor, "Document.Read", "Document", documentId, cancellationToken: cancellationToken);
        Document document = await database.Documents.AsNoTracking().SingleOrDefaultAsync(document => document.TenantId == tenantId
            && document.Id == documentId, cancellationToken) ?? throw new NotFoundException("Document not found.");
        if (document.DocumentType != "FinancialSource" || document.Classification != DocumentClassification.Restricted)
            throw new PermissionDeniedException("Document.Read", "Financial intake requires a restricted financial source document.");
        var evidence = await database.DocumentFiles.AsNoTracking().Where(file => file.TenantId == tenantId
            && file.DocumentId == documentId && file.SourceEvidenceJson != null).Select(file => new { file.SourceEvidenceJson, file.Sha256 }).ToListAsync(cancellationToken);
        foreach (var file in evidence)
        {
            FinancialDocumentSource source = System.Text.Json.JsonSerializer.Deserialize<FinancialDocumentSource>(file.SourceEvidenceJson!, FinancialDocumentActions.JsonOptions)
                ?? throw new PermissionDeniedException("Document.Read");
            if (sources is null || source.ContentHash != file.Sha256) throw new PermissionDeniedException("Integration.Use");
            await sources.RequireAsync(source, actor, cancellationToken);
        }
        return document;
    }
}
