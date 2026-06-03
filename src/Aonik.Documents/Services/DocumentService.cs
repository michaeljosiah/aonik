using System.Text.Json;
using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Compliance; // Document/DocumentFile — namespace preserved (Spec 035)
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Documents.Services;

/// <summary>
/// Generic-document service for <c>Aonik.Documents</c> (Spec 035 §11/§17). Implements the
/// SharedKernel reader + writer contracts over <see cref="DocumentsDbContext"/> and
/// <see cref="IDocumentFileStore"/>. Compliance verification (DocumentUsage / DocumentVerification)
/// is deliberately NOT here — it stays in Aonik.Platform and resolves documents via this reader.
/// </summary>
internal sealed class DocumentService : IDocumentReader, IDocumentWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDocumentFileStore _documentFileStore;

    public DocumentService(
        DocumentsDbContext dbContext,
        ITenantProvider tenantProvider,
        IDocumentFileStore documentFileStore)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _documentFileStore = documentFileStore;
    }

    // ── IDocumentWriter ──────────────────────────────────────────────────

    public async Task<DocumentDto> CreateDocumentAsync(
        CreateDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.DocumentType))
            throw new ArgumentException("Document type is required.", nameof(command));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var classification = command.Classification ?? DocumentClassification.Internal;

        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OwnerPartyId = command.OwnerPartyId,
            DocumentType = command.DocumentType.Trim(),
            Status = string.IsNullOrWhiteSpace(command.Status) ? "Draft" : command.Status!.Trim(),
            Classification = classification,
            Source = string.IsNullOrWhiteSpace(command.Source) ? "CustomerUpload" : command.Source!.Trim(),
            IndexStatus = ResolveInitialIndexStatus(classification),
            IssuedOn = command.IssuedOn,
            ExpiresOn = command.ExpiresOn,
            IssuerName = command.IssuerName?.Trim(),
            CountryCode = command.CountryCode?.Trim(),
            ReferenceNumber = command.ReferenceNumber?.Trim(),
            TagsJson = SerializeTags(command.Tags),
            AttributesJson = string.IsNullOrWhiteSpace(command.AttributesJson) ? "{}" : command.AttributesJson!,
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapDocument(document);
    }

    public async Task<DocumentFileDto> UploadFileAsync(
        UploadFileCommand command,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(command.FileName))
            throw new ArgumentException("File name is required.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.ContentType))
            throw new ArgumentException("Content type is required.", nameof(command));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == command.DocumentId && d.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Document {command.DocumentId} not found.");

        var upload = await _documentFileStore.UploadDocumentFileAsync(
            tenantId, document.Id, content, command.FileName, command.ContentType, cancellationToken);

        var file = new DocumentFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = document.Id,
            StorageProvider = upload.StorageProvider.Trim(),
            StorageContainer = upload.StorageContainer?.Trim(),
            StorageKey = upload.StorageKey.Trim(),
            ContentType = upload.ContentType.Trim(),
            FileName = upload.FileName.Trim(),
            FileSizeBytes = upload.FileSizeBytes,
            Sha256 = upload.Sha256.Trim(),
            PageIndex = command.PageIndex,
            Side = command.Side?.Trim(),
            CapturedAt = command.CapturedAt,
            CapturedBy = command.CapturedBy?.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(command.MetadataJson) ? "{}" : command.MetadataJson!,
            ExtractedTextStatus = ResolveExtractedTextStatus(upload.ContentType),
        };

        _dbContext.DocumentFiles.Add(file);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Phase 3 (Spec 035 §13): publish DocumentUploadedEvent here so the async ingestion
        // pipeline embeds indexable files. Wired when the Worker job lands.
        return MapFile(file);
    }

    // ── IDocumentReader ──────────────────────────────────────────────────

    public async Task<DocumentDto?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var document = await _dbContext.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);
        return document is null ? null : MapDocument(document);
    }

    public async Task<PagedResult<DocumentListItem>> ListDocumentsAsync(
        ListDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var q = _dbContext.Documents.AsNoTracking().Where(d => d.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.DocumentType))
        {
            var t = query.DocumentType.Trim();
            q = q.Where(d => d.DocumentType == t);
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var s = query.Status.Trim();
            q = q.Where(d => d.Status == s);
        }
        if (query.OwnerPartyId.HasValue)
            q = q.Where(d => d.OwnerPartyId == query.OwnerPartyId.Value);
        if (query.Classification.HasValue)
            q = q.Where(d => d.Classification == query.Classification.Value);
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var pattern = $"%{query.Tag.Trim()}%";
            q = q.Where(d => EF.Functions.Like(d.TagsJson, pattern));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(d =>
                EF.Functions.Like(d.DocumentType, pattern) ||
                (d.ReferenceNumber != null && EF.Functions.Like(d.ReferenceNumber, pattern)) ||
                (d.IssuerName != null && EF.Functions.Like(d.IssuerName, pattern)));
        }

        var totalCount = await q.CountAsync(cancellationToken);

        var rows = await q
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.OwnerPartyId,
                d.DocumentType,
                d.Classification,
                d.Status,
                d.IndexStatus,
                d.IssuedOn,
                d.ExpiresOn,
                d.CreatedAt,
                FilesCount = _dbContext.DocumentFiles.Count(f => f.DocumentId == d.Id),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new DocumentListItem(
            r.Id, r.OwnerPartyId, r.DocumentType, r.Classification, r.Status, r.IndexStatus,
            r.IssuedOn, r.ExpiresOn, r.FilesCount, r.CreatedAt)).ToList();

        return new PagedResult<DocumentListItem>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<DocumentFileDto>> GetFilesAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var files = await _dbContext.DocumentFiles.AsNoTracking()
            .Where(f => f.DocumentId == documentId && f.TenantId == tenantId)
            .OrderBy(f => f.PageIndex)
            .ToListAsync(cancellationToken);
        return files.Select(MapFile).ToList();
    }

    public Task<Uri> GetReadUrlAsync(
        Guid documentFileId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "Signed read URLs are wired in a later phase of Spec 035 (§11).");

    // ── helpers ──────────────────────────────────────────────────────────

    private static DocumentIndexStatus ResolveInitialIndexStatus(DocumentClassification classification)
        => classification is DocumentClassification.Restricted or DocumentClassification.Sensitive
            ? DocumentIndexStatus.NotIndexable
            : DocumentIndexStatus.Pending;

    private static ExtractedTextStatus ResolveExtractedTextStatus(string contentType)
        => contentType switch
        {
            "text/plain" => ExtractedTextStatus.Native,
            "application/pdf" => ExtractedTextStatus.Native,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ExtractedTextStatus.Native,
            _ when contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => ExtractedTextStatus.OcrRequired,
            _ => ExtractedTextStatus.Unsupported,
        };

    private static DocumentDto MapDocument(Document d) => new(
        d.Id, d.OwnerPartyId, d.DocumentType, d.Classification, d.Status, d.Source, d.IndexStatus, d.IndexedAt,
        d.IssuedOn, d.ExpiresOn, d.IssuerName, d.CountryCode, d.ReferenceNumber,
        DeserializeTags(d.TagsJson), d.AttributesJson, d.CreatedAt, d.UpdatedAt);

    private static DocumentFileDto MapFile(DocumentFile f) => new(
        f.Id, f.DocumentId, f.StorageProvider, f.StorageContainer, f.StorageKey, f.ContentType,
        f.FileName, f.FileSizeBytes, f.Sha256, f.PageIndex, f.Side, f.CreatedAt);

    private static string SerializeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
            return "[]";
        var normalized = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static IReadOnlyList<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
            return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
