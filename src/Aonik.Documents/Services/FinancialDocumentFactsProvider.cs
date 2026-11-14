using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Documents.Persistence;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Documents.Services;

internal sealed class FinancialDocumentFactsProvider(DocumentsDbContext database, ITenantProvider tenantProvider,
    FinancialDocumentAuthorization authorization, IDocumentFileStore storage) : IWorkflowFactsProvider
{
    public string SubjectType => FinancialDocumentActions.SubjectType;
    public string Schema => FinancialDocumentActions.FactSchema;

    public Task<WorkflowConnectionDependency?> GetConnectionDependencyAsync(Guid subjectId, WorkflowActor actor, CancellationToken cancellationToken = default)
        => authorization.GetConnectionDependencyAsync(subjectId, actor, cancellationToken);

    public async Task<WorkflowFactsSnapshot?> LoadAsync(Guid subjectId, WorkflowActor actor, CancellationToken cancellationToken = default)
    {
        var document = await authorization.RequireAsync(subjectId, actor, cancellationToken);
        var files = await database.DocumentFiles.AsNoTracking().Where(file => file.TenantId == tenantProvider.GetCurrentTenantId()
            && file.DocumentId == subjectId).OrderBy(file => file.Id).Take(11).ToListAsync(cancellationToken);
        if (files.Count > 10) throw new InvalidOperationException("FinancialSourceFileLimit");
        foreach (var file in files) _ = await FinancialDocumentService.ReadBytesAsync(storage, file, cancellationToken);
        string version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { document.Id, document.Status, document.Classification, document.OwnerPartyId,
            files = files.Select(file => new { file.Id, file.Sha256, file.ContentType, file.FileSizeBytes, file.StorageKey, file.SourceFingerprint, file.SourceEvidenceJson }) }))));
        return new(Schema, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["document.id"] = JsonSerializer.SerializeToElement(document.Id),
            ["document.fileId"] = JsonSerializer.SerializeToElement(files.Count == 1 ? files[0].Id : Guid.Empty),
            ["document.ownerPartyId"] = JsonSerializer.SerializeToElement(document.OwnerPartyId),
        }, version, files.Count != 1 ? "FinancialDocumentRequiresOneFile" : null, document.CreatedBy);
    }
}
