using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Documents.Entities;
using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Documents.Services;

internal sealed class FinancialDocumentService(DocumentsDbContext database, ITenantProvider tenantProvider,
    FinancialDocumentAuthorization authorization, IDocumentFileStore files, IDocumentTextExtractor extractor,
    IDocumentSafetyScanner scanner, IFinancialDocumentParser parser) : IFinancialDocumentReader
{
    internal const string ExtractionType = "FinancialCandidate.v1";

    public async Task<IReadOnlyList<FinancialDocumentExtractionReceipt>> ListAsync(Guid documentId, WorkflowActor actor,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(documentId, actor, cancellationToken);
        List<string> outputs = await database.DocumentExtractions.AsNoTracking().Where(extraction => extraction.TenantId == tenantProvider.GetCurrentTenantId()
            && extraction.DocumentId == documentId && extraction.ExtractionType == ExtractionType && extraction.Status != "Erased")
            .OrderByDescending(extraction => extraction.CreatedAt).Take(100).Select(extraction => extraction.OutputJson).ToListAsync(cancellationToken);
        return outputs.Select(json => JsonSerializer.Deserialize<FinancialDocumentCandidate>(json, FinancialDocumentActions.JsonOptions)!)
            .Select(candidate => new FinancialDocumentExtractionReceipt(candidate.ExtractionId, candidate.DocumentId, candidate.CandidateHash,
                candidate.Status, candidate.Issues, candidate.AiRunId)).ToArray();
    }

    public async Task<FinancialDocumentFileContent> OpenFileAsync(Guid documentId, Guid fileId, WorkflowActor actor,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(documentId, actor, cancellationToken);
        DocumentFile file = await FileAsync(documentId, fileId, cancellationToken);
        byte[] bytes = await BytesAsync(file, cancellationToken);
        string name = Path.GetFileName(file.FileName ?? "document");
        name = new string(name.Where(character => !char.IsControl(character)).Take(128).ToArray());
        return new(new MemoryStream(bytes, writable: false), name, file.ContentType);
    }

    public async Task<FinancialDocumentCandidate?> FindAsync(ExtractFinancialDocumentArguments input, WorkflowActor actor,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(input.DocumentId, actor, cancellationToken);
        DocumentFile file = await FileAsync(input.DocumentId, input.FileId, cancellationToken);
        string inputHash = Hash($"{FinancialDocumentActions.UseCase}:{file.Id:D}:{file.Sha256}");
        Guid? id = await database.DocumentExtractions.AsNoTracking().Where(extraction => extraction.TenantId == tenantProvider.GetCurrentTenantId()
            && extraction.DocumentId == input.DocumentId && extraction.ExtractionType == ExtractionType && extraction.InputHash == inputHash)
            .Select(extraction => (Guid?)extraction.Id).SingleOrDefaultAsync(cancellationToken);
        return id is { } existing ? await RequireAsync(existing, actor, cancellationToken) : null;
    }

    public async Task<FinancialDocumentCandidate> ExtractAsync(ExtractFinancialDocumentArguments input, WorkflowActor actor,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequireExtractionAsync(input.DocumentId, actor, cancellationToken);
        DocumentFile file = await FileAsync(input.DocumentId, input.FileId, cancellationToken);
        string inputHash = Hash($"{FinancialDocumentActions.UseCase}:{file.Id:D}:{file.Sha256}");
        DocumentExtraction? previous = await database.DocumentExtractions.AsNoTracking().SingleOrDefaultAsync(extraction =>
            extraction.TenantId == tenantProvider.GetCurrentTenantId() && extraction.DocumentId == input.DocumentId
            && extraction.ExtractionType == ExtractionType && extraction.InputHash == inputHash, cancellationToken);
        if (previous is not null) return await RequireAsync(previous.Id, actor, cancellationToken);
        byte[] bytes = await BytesAsync(file, cancellationToken);
        await scanner.ScanAsync(bytes, cancellationToken);
        using var content = new MemoryStream(bytes, writable: false);
        DocumentTextExtractionResult text = await extractor.ExtractTextAsync(content, file.ContentType, cancellationToken);
        if (!text.HasText || text.Text.Length > 100_000) throw new FinancialDocumentException("FinancialDocumentTextUnavailable");
        ParsedFinancialDocument parsed = await parser.ParseAsync(input.DocumentId, file.Id, file.Sha256!, text.Text, cancellationToken);
        if (parsed.AiRunId == Guid.Empty || string.IsNullOrWhiteSpace(parsed.ModelId) || parsed.PromptHash.Length != 64)
            throw new FinancialDocumentException("FinancialExtractionAuditMissing");
        await authorization.RequireAsync(input.DocumentId, actor, cancellationToken);
        Document document = await database.Documents.SingleAsync(document => document.TenantId == tenantProvider.GetCurrentTenantId()
            && document.Id == input.DocumentId, cancellationToken);
        DocumentFile current = await FileAsync(input.DocumentId, input.FileId, cancellationToken);
        if (current.Sha256 != file.Sha256 || current.SourceEvidenceJson != file.SourceEvidenceJson || current.StorageKey != file.StorageKey)
            throw new FinancialDocumentException("FinancialDocumentSourceChanged");
        IReadOnlyList<string> issues = FinancialDocumentValidation.Issues(parsed.Fields, text.Provenance)
            .Concat(FinancialDocumentValidation.GroundingIssues(parsed.Fields, text.Text)).Distinct(StringComparer.Ordinal).ToArray();
        Guid id = Guid.NewGuid();
        var candidate = new FinancialDocumentCandidate(id, document.Id, file.Id, file.Sha256!, "", issues.Count == 0 ? "ReadyForReview" : "NeedsManualReview",
            parsed.Fields, issues, parsed.AiRunId, parsed.ModelId, parsed.PromptHash, text.Provenance,
            file.SourceEvidenceJson is null ? null : JsonSerializer.Deserialize<FinancialDocumentSource>(file.SourceEvidenceJson, FinancialDocumentActions.JsonOptions), actor);
        candidate = candidate with { CandidateHash = CandidateHash(candidate) };
        database.DocumentExtractions.Add(new DocumentExtraction
        {
            Id = id, TenantId = document.TenantId, DocumentId = document.Id, DocumentFileId = file.Id, InputHash = inputHash,
            ExtractionType = ExtractionType, OutputJson = JsonSerializer.Serialize(candidate, FinancialDocumentActions.JsonOptions),
            Status = candidate.Status, Confidence = double.IsFinite(parsed.Fields.Confidence) ? parsed.Fields.Confidence : null, AiRunId = parsed.AiRunId,
        });
        // Fence erasure/source edits while committing immutable extraction evidence. The semantic facts stay unchanged.
        database.Entry(document).Property(document => document.Status).IsModified = true;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception is not DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            DocumentExtraction? winner = await database.DocumentExtractions.AsNoTracking().SingleOrDefaultAsync(extraction => extraction.TenantId == tenantProvider.GetCurrentTenantId()
                && extraction.DocumentId == input.DocumentId && extraction.ExtractionType == ExtractionType && extraction.InputHash == inputHash, cancellationToken);
            if (winner is null) throw;
            return await RequireAsync(winner.Id, actor, cancellationToken);
        }
        return candidate;
    }

    public async Task<FinancialDocumentCandidate> RequireAsync(Guid extractionId, WorkflowActor actor, CancellationToken cancellationToken = default)
    {
        await authorization.RequireReaderAsync(actor, cancellationToken);
        DocumentExtraction extraction = await database.DocumentExtractions.AsNoTracking().SingleOrDefaultAsync(extraction =>
            extraction.TenantId == tenantProvider.GetCurrentTenantId() && extraction.Id == extractionId && extraction.ExtractionType == ExtractionType,
            cancellationToken) ?? throw new NotFoundException("Financial candidate not found.");
        await authorization.RequireAsync(extraction.DocumentId, actor, cancellationToken);
        if (extraction.Status == "Erased") throw new NotFoundException("Financial candidate not found.");
        FinancialDocumentCandidate candidate = JsonSerializer.Deserialize<FinancialDocumentCandidate>(extraction.OutputJson, FinancialDocumentActions.JsonOptions)
            ?? throw new FinancialDocumentException("FinancialCandidateInvalid");
        DocumentFile file = await FileAsync(candidate.DocumentId, candidate.FileId, cancellationToken);
        if (candidate.ExtractionId != extraction.Id || candidate.DocumentId != extraction.DocumentId || candidate.FileId != extraction.DocumentFileId
            || candidate.SourceHash != file.Sha256 || candidate.CandidateHash != CandidateHash(candidate) || candidate.AiRunId != extraction.AiRunId
            || candidate.Status != extraction.Status || candidate.Source != (file.SourceEvidenceJson is null ? null
                : JsonSerializer.Deserialize<FinancialDocumentSource>(file.SourceEvidenceJson, FinancialDocumentActions.JsonOptions)))
            throw new FinancialDocumentException("FinancialCandidateSourceChanged");
        _ = await BytesAsync(file, cancellationToken);
        return candidate;
    }

    private async Task<DocumentFile> FileAsync(Guid documentId, Guid fileId, CancellationToken cancellationToken)
    {
        DocumentFile file = await database.DocumentFiles.AsNoTracking().SingleOrDefaultAsync(file => file.TenantId == tenantProvider.GetCurrentTenantId()
            && file.DocumentId == documentId && file.Id == fileId, cancellationToken) ?? throw new NotFoundException("Financial source file not found.");
        if (file.Sha256 is not { Length: 64 } || !file.StorageKey.StartsWith("private:", StringComparison.Ordinal)
            || file.ContentType is not ("text/plain" or "application/pdf" or "image/png" or "image/jpeg") || file.FileSizeBytes is null or <= 0 or > 10 * 1024 * 1024)
            throw new FinancialDocumentException("FinancialSourceUnsupported");
        return file;
    }

    public async Task<FinancialDocumentCandidate> RequireContinuationAsync(Guid extractionId, WorkflowActor actor, CancellationToken cancellationToken = default)
        => await RequireAsync(extractionId, await ContinuationActorAsync(extractionId, actor, cancellationToken), cancellationToken);

    public async Task<WorkflowConnectionDependency?> GetConnectionDependencyAsync(Guid extractionId, WorkflowActor actor, CancellationToken cancellationToken = default)
    {
        actor = await ContinuationActorAsync(extractionId, actor, cancellationToken);
        Guid documentId = await database.DocumentExtractions.AsNoTracking().Where(extraction => extraction.TenantId == tenantProvider.GetCurrentTenantId()
            && extraction.Id == extractionId && extraction.ExtractionType == ExtractionType && extraction.Status != "Erased")
            .Select(extraction => (Guid?)extraction.DocumentId).SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Financial candidate not found.");
        return await authorization.GetConnectionDependencyAsync(documentId, actor, cancellationToken);
    }

    private async Task<WorkflowActor> ContinuationActorAsync(Guid extractionId, WorkflowActor actor, CancellationToken cancellationToken)
    {
        if (actor.Type == "AutomationIdentity")
        {
            string json = await database.DocumentExtractions.AsNoTracking().Where(extraction => extraction.TenantId == tenantProvider.GetCurrentTenantId()
                && extraction.Id == extractionId && extraction.ExtractionType == ExtractionType && extraction.Status != "Erased")
                .Select(extraction => extraction.OutputJson).SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Financial candidate not found.");
            var candidate = JsonSerializer.Deserialize<FinancialDocumentCandidate>(json, FinancialDocumentActions.JsonOptions);
            if (candidate?.ExtractedBy is not { Type: "AutomationIdentity", WorkflowVersionId: not null } original || original.Id != actor.Id)
                throw new PermissionDeniedException("Document.Read");
            // The child needs its own current Invoice grant AND the original actor's still-current Document/source grant.
            actor = actor with { WorkflowVersionId = original.WorkflowVersionId };
        }
        return actor;
    }
    private Task<byte[]> BytesAsync(DocumentFile file, CancellationToken cancellationToken) => ReadBytesAsync(files, file, cancellationToken);

    internal static async Task<byte[]> ReadBytesAsync(IDocumentFileStore files, DocumentFile file, CancellationToken cancellationToken)
    {
        if (file.FileSizeBytes is null or <= 0 or > 10 * 1024 * 1024 || !file.StorageKey.StartsWith("private:", StringComparison.Ordinal))
            throw new FinancialDocumentException("FinancialSourceUnsupported");
        await using Stream stream = await files.OpenReadAsync(file.StorageKey, cancellationToken);
        using var output = new MemoryStream(); byte[] buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > 10 * 1024 * 1024) throw new FinancialDocumentException("FinancialSourceSizeLimit");
            output.Write(buffer, 0, read);
        }
        byte[] bytes = output.ToArray();
        if (bytes.Length != file.FileSizeBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256)
            throw new FinancialDocumentException("FinancialSourceHashMismatch");
        return bytes;
    }
    private static string CandidateHash(FinancialDocumentCandidate candidate)
        => Hash(JsonSerializer.Serialize(candidate with { CandidateHash = "" }, FinancialDocumentActions.JsonOptions));
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
