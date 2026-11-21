using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Documents.Services;

internal sealed class FinancialDocumentMailboxIntake(DocumentsDbContext database, ITenantProvider tenantProvider,
    IFinancialDocumentMailboxReader reader, IWorkflowMailboxBindingReader bindings, IFinancialDocumentSourceAccess authority,
    IAutomationIdentityAuthorization automation, IDocumentFileStore files, IDocumentSafetyScanner scanner, IPartyReader parties,
    IAuditLogWriter audit) : IFinancialDocumentMailboxIntake
{
    public async Task<IReadOnlyList<Guid>> AdmitAsync(Guid bindingId, int revision, string messageId, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        ConnectorSubscriptionBinding binding = await bindings.RequireActiveAsync(bindingId, revision, cancellationToken);
        if (binding.SubjectType != WorkflowSubjects.Document) throw new PermissionDeniedException("Document.Read");
        var actor = new WorkflowActor("AutomationIdentity", binding.AutomationIdentityId, WorkflowVersionId: binding.WorkflowVersionId);
        await automation.RequireAllSubjectsAsync(actor, WorkflowSubjects.Document, cancellationToken);
        Guid ownerId = await parties.GetTenantPartyIdAsync(tenantId, cancellationToken) ?? throw new FinancialDocumentException("TenantDocumentOwnerMissing");
        IReadOnlyList<FinancialDocumentAttachment> attachments = await reader.ReadAsync(bindingId, revision, messageId, cancellationToken);
        if (attachments.Count > 5 || attachments.Any(attachment => attachment.Bytes.Length is < 1 or > 10 * 1024 * 1024)
            || attachments.Sum(attachment => (long)attachment.Bytes.Length) > 10 * 1024 * 1024)
            throw new FinancialDocumentException("FinancialSourceAttachmentLimit");
        List<Guid> admitted = [];
        foreach (FinancialDocumentAttachment attachment in attachments)
        {
            FinancialDocumentSource source = attachment.Source;
            if (source.BindingId != bindingId || source.BindingRevision != revision || source.MessageId != messageId
                || source.WorkflowId != binding.WorkflowId || source.WorkflowVersionId != binding.WorkflowVersionId
                || source.ConnectionId != binding.Connection.ConnectionId || source.WorkflowGrantId != binding.Connection.WorkflowGrantId
                || source.AutomationIdentityId != actor.Id || Hash(attachment.Bytes) != source.ContentHash
                || attachment.ContentType is not ("application/pdf" or "image/png" or "image/jpeg" or "text/plain"))
                throw new FinancialDocumentException("FinancialSourceAttachmentMismatch");
            await authority.RequireAsync(source, actor, cancellationToken);
            string fingerprint = Hash(JsonSerializer.SerializeToUtf8Bytes(new { tenantId, source.ConnectionId, source.ProviderAccountId,
                source.MessageId, source.AttachmentId, source.ContentHash }, FinancialDocumentActions.JsonOptions));
            DocumentFile? prior = await FindAsync(fingerprint, tenantId, cancellationToken);
            if (prior is not null)
            {
                Guid? priorId = await ExistingAsync(prior, source, tenantId, cancellationToken);
                if (priorId.HasValue) admitted.Add(priorId.Value);
                continue;
            }
            await scanner.ScanAsync(attachment.Bytes, cancellationToken);
            var document = new Document { TenantId = tenantId, OwnerPartyId = ownerId, DocumentType = "FinancialSource", Status = "Active",
                Classification = DocumentClassification.Restricted, Source = "Gmail", IndexStatus = DocumentIndexStatus.NotIndexable };
            string name = new(attachment.FileName.Replace('\\', '/').Split('/').Last().Where(character => !char.IsControl(character)).Take(160).ToArray());
            if (string.IsNullOrWhiteSpace(name)) name = "attachment";
            using var bytes = new MemoryStream(attachment.Bytes, writable: false);
            DocumentFileUploadResult uploaded = await files.UploadPrivateDocumentFileAsync(tenantId, document.Id, bytes, name, attachment.ContentType, cancellationToken);
            var file = new DocumentFile { TenantId = tenantId, DocumentId = document.Id, StorageProvider = uploaded.StorageProvider,
                StorageContainer = uploaded.StorageContainer, StorageKey = uploaded.StorageKey, ContentType = uploaded.ContentType,
                FileName = uploaded.FileName, FileSizeBytes = uploaded.FileSizeBytes, Sha256 = uploaded.Sha256, SourceFingerprint = fingerprint,
                SourceEvidenceJson = JsonSerializer.Serialize(source, FinancialDocumentActions.JsonOptions),
                ExtractedTextStatus = attachment.ContentType == "text/plain" ? ExtractedTextStatus.Native : ExtractedTextStatus.OcrRequired };
            try
            {
                if (!uploaded.StorageKey.StartsWith("private:", StringComparison.Ordinal) || uploaded.Sha256 != source.ContentHash
                    || uploaded.FileSizeBytes != attachment.Bytes.Length || uploaded.ContentType != attachment.ContentType)
                    throw new FinancialDocumentException("FinancialSourceStorageMismatch");
                _ = await bindings.RequireActiveAsync(bindingId, revision, cancellationToken);
                await automation.RequireAllSubjectsAsync(actor, WorkflowSubjects.Document, cancellationToken);
                await authority.RequireAsync(source, actor, cancellationToken);
            }
            catch
            {
                await files.DeleteAsync(uploaded.StorageKey, CancellationToken.None);
                throw;
            }
            database.Documents.Add(document); database.DocumentFiles.Add(file);
            try { await database.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                database.Entry(file).State = EntityState.Detached; database.Entry(document).State = EntityState.Detached;
                DocumentFile? winner = await FindAsync(fingerprint, tenantId, cancellationToken);
                if (winner is null) throw;
                // A confirmed competing receipt owns its own blob. An uncertain commit is never cleaned up speculatively.
                if (winner.Id != file.Id) await files.DeleteAsync(uploaded.StorageKey, CancellationToken.None);
                Guid? winnerId = await ExistingAsync(winner, source, tenantId, cancellationToken);
                if (winnerId.HasValue) admitted.Add(winnerId.Value);
                continue;
            }
            await audit.LogActorAsync("Document.FinancialSourceAdmitted", "Document", document.Id, tenantId, actor.Type, actor.Id,
                null, JsonSerializer.Serialize(new { bindingId, revision, source.ConnectionId, source.WorkflowVersionId, fileId = file.Id, fingerprint },
                    FinancialDocumentActions.JsonOptions), cancellationToken);
            admitted.Add(document.Id);
        }
        return admitted.Distinct().ToArray();
    }

    private Task<DocumentFile?> FindAsync(string fingerprint, Guid tenantId, CancellationToken cancellationToken)
        => database.DocumentFiles.IncludeSoftDeleted().AsNoTracking().SingleOrDefaultAsync(file => file.TenantId == tenantId
            && file.SourceFingerprint == fingerprint, cancellationToken);

    private async Task<Guid?> ExistingAsync(DocumentFile file, FinancialDocumentSource source, Guid tenantId, CancellationToken cancellationToken)
    {
        if (file.IsDeleted || !await database.Documents.AsNoTracking().AnyAsync(document => document.TenantId == tenantId && document.Id == file.DocumentId, cancellationToken)) return null;
        if (file.SourceEvidenceJson != JsonSerializer.Serialize(source, FinancialDocumentActions.JsonOptions))
            throw new FinancialDocumentException("FinancialSourceAlreadyBound");
        return file.DocumentId;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
