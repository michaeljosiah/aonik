using System.Text.Json;
using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Compliance; // Document/DocumentFile — namespace preserved (Spec 035)
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Integration;
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

    // Roles that may read/write documents tenant-wide (staff / operations / admin). A caller who is
    // a PersonalUser WITHOUT any of these is a customer, scoped to their own owner party. A caller
    // with no roles at all (a trusted system/Worker context — no external request reaches here
    // without a role, since the endpoints gate on a policy) is also treated as tenant-wide.
    private static readonly string[] TenantWideRoles =
        { "PlatformAdmin", "TenantAdmin", "Operations", "ReadOnly" };
    private const string CustomerRole = "PersonalUser";

    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDocumentFileStore _documentFileStore;
    private readonly IDocumentVectorIndex _vectorIndex;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserPartyResolver _userPartyResolver;

    public DocumentService(
        DocumentsDbContext dbContext,
        ITenantProvider tenantProvider,
        IDocumentFileStore documentFileStore,
        IDocumentVectorIndex vectorIndex,
        ICurrentUserContext currentUserContext,
        IUserPartyResolver userPartyResolver)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _documentFileStore = documentFileStore;
        _vectorIndex = vectorIndex;
        _currentUserContext = currentUserContext;
        _userPartyResolver = userPartyResolver;
    }

    // ── IDocumentWriter ──────────────────────────────────────────────────

    public async Task<DocumentDto> CreateDocumentAsync(
        CreateDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.DocumentType))
            throw new ArgumentException("Document type is required.", nameof(command));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);

        // A customer can only create documents owned by themselves — the owner party is taken from
        // authenticated context, never from request input, so they cannot create a document "owned"
        // by another party. Staff/system callers create on behalf of the supplied owner party.
        var ownerPartyId = command.OwnerPartyId;
        if (!scope.TenantWide)
        {
            if (scope.OwnerPartyId is null)
                throw new InvalidOperationException(
                    "The current user is not linked to a party and cannot create documents.");
            ownerPartyId = scope.OwnerPartyId.Value;
        }

        // Classification defaults from DocumentType when omitted (Spec 035 §10). The fallback is
        // Personal (owner-scoped), never a tenant-wide class — so an unclassified customer upload
        // such as a tax return or statement is never indexed tenant-wide.
        var classification = command.Classification ?? DefaultClassificationForType(command.DocumentType);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OwnerPartyId = ownerPartyId,
            DocumentType = command.DocumentType.Trim(),
            Status = string.IsNullOrWhiteSpace(command.Status) ? "Draft" : command.Status!.Trim(),
            Title = string.IsNullOrWhiteSpace(command.Title) ? null : command.Title!.Trim(),
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
        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == command.DocumentId && d.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Document {command.DocumentId} not found.");

        // A customer may only upload into their own documents. Surface "not found" rather than a
        // distinct "forbidden" so a customer cannot probe for the existence of others' documents.
        if (!scope.TenantWide && document.OwnerPartyId != scope.OwnerPartyId)
            throw new InvalidOperationException($"Document {command.DocumentId} not found.");

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

        // Phase 3 (Spec 035 §13): for an indexable document, raise DocumentUploadedEvent in the
        // same transaction via the outbox so the async ingestion pipeline embeds the file without
        // blocking this upload. Restricted/Sensitive/NotIndexable documents are never auto-indexed,
        // so they raise no event — no embedding cost and no handler work. Enqueue BEFORE
        // SaveChanges so the outbox row commits atomically with the file.
        if (document.IndexStatus == DocumentIndexStatus.Pending)
        {
            _dbContext.EnqueueIntegrationEvent(new DocumentUploadedEvent(
                TenantId: tenantId,
                DocumentId: document.Id,
                DocumentFileId: file.Id,
                OwnerPartyId: document.OwnerPartyId,
                Classification: document.Classification,
                ContentType: file.ContentType));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapFile(file);
    }

    public async Task DeleteDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Document {documentId} not found.");

        var files = await _dbContext.DocumentFiles
            .Where(f => f.DocumentId == documentId && f.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        // Erasure ordering prioritises the privacy invariant (Spec 035 §15). Purge the vectors
        // FIRST so retrieval can never return this document again, then remove the blob object(s),
        // then soft-delete the rows + publish DocumentDeletedEvent atomically. Each external step is
        // idempotent (purge re-scrolls to empty; blob delete no-ops on a missing object), so a
        // failure between steps is safe to retry — and the worst interrupted state leaves vectors
        // already gone, never an orphaned searchable vector.
        await _vectorIndex.PurgeDocumentAsync(documentId, cancellationToken);

        foreach (var file in files)
        {
            await _documentFileStore.DeleteAsync(file.StorageKey, cancellationToken);
        }

        // Remove → soft-delete (the base converts the delete to IsDeleted/DeletedAt).
        _dbContext.DocumentFiles.RemoveRange(files);
        _dbContext.Documents.Remove(document);

        _dbContext.EnqueueIntegrationEvent(
            new DocumentDeletedEvent(tenantId, documentId, document.OwnerPartyId));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ── IDocumentReader ──────────────────────────────────────────────────

    public async Task<DocumentDto?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);
        if (scope.IsDeniedCustomer)
            return null;

        var document = await _dbContext.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);
        if (document is null)
            return null;

        // A customer can only read their own documents; another party's id is a 404 for them.
        if (!scope.TenantWide && document.OwnerPartyId != scope.OwnerPartyId)
            return null;

        return MapDocument(document);
    }

    public async Task<PagedResult<DocumentListItem>> ListDocumentsAsync(
        ListDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);
        if (scope.IsDeniedCustomer)
            return new PagedResult<DocumentListItem>(new List<DocumentListItem>(), 0, pageNumber, pageSize);

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

        // A customer is forced to their own owner party; a request-supplied OwnerPartyId is ignored
        // for them and can never widen the result set. Staff/system callers may filter by the
        // requested owner party (or omit it for a tenant-wide listing).
        var effectiveOwnerPartyId = scope.TenantWide ? query.OwnerPartyId : scope.OwnerPartyId;
        if (effectiveOwnerPartyId.HasValue)
            q = q.Where(d => d.OwnerPartyId == effectiveOwnerPartyId.Value);
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

        // Spec 046 Vault filters: documents linked to a CareEntity, and by year.
        if (query.CareEntityId is Guid careEntityId)
        {
            q = q.Where(d => _dbContext.DocumentLinks.Any(l =>
                l.TenantId == tenantId
                && l.DocumentId == d.Id
                && l.TargetType == "careEntity"
                && l.TargetId == careEntityId));
        }
        if (query.Year is int year)
        {
            q = q.Where(d =>
                (d.IssuedOn != null && d.IssuedOn.Value.Year == year)
                || (d.IssuedOn == null && d.CreatedAt.Year == year));
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
        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);
        if (scope.IsDeniedCustomer)
            return Array.Empty<DocumentFileDto>();

        // A customer may only read files of their own documents.
        if (!scope.TenantWide)
        {
            var owns = await _dbContext.Documents.AsNoTracking().AnyAsync(
                d => d.Id == documentId && d.TenantId == tenantId && d.OwnerPartyId == scope.OwnerPartyId,
                cancellationToken);
            if (!owns)
                return Array.Empty<DocumentFileDto>();
        }

        var files = await _dbContext.DocumentFiles.AsNoTracking()
            .Where(f => f.DocumentId == documentId && f.TenantId == tenantId)
            .OrderBy(f => f.PageIndex)
            .ToListAsync(cancellationToken);
        return files.Select(MapFile).ToList();
    }

    public async Task<Uri> GetReadUrlAsync(
        Guid documentFileId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);
        if (scope.IsDeniedCustomer)
            throw new InvalidOperationException($"Document file '{documentFileId}' was not found.");

        var query = _dbContext.DocumentFiles.AsNoTracking()
            .Where(f => f.Id == documentFileId && f.TenantId == tenantId);

        // A customer may only read files of their own documents — mirror the GetFilesAsync scope check.
        if (!scope.TenantWide)
        {
            query = query.Where(f => _dbContext.Documents.Any(
                d => d.Id == f.DocumentId && d.TenantId == tenantId && d.OwnerPartyId == scope.OwnerPartyId));
        }

        var storageKey = await query.Select(f => f.StorageKey).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new InvalidOperationException($"Document file '{documentFileId}' was not found.");

        // URL convention matches customer profile photos (PublicBaseUrl / dev static middleware).
        return _documentFileStore.GetReadUrl(storageKey);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// The effective document scope for the current caller. Staff/system callers see the whole
    /// tenant; a customer (a <c>PersonalUser</c> with no staff role) is restricted to their own
    /// owner party — derived from authenticated context, never from request input (Spec 035 §14 /
    /// R7). A customer who is not linked to a party is denied (reads return empty; writes throw).
    /// </summary>
    private readonly record struct CallerScope(bool TenantWide, Guid? OwnerPartyId)
    {
        public bool IsDeniedCustomer => !TenantWide && OwnerPartyId is null;
    }

    private async Task<CallerScope> ResolveCallerScopeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var roles = _currentUserContext.Roles ?? Array.Empty<string>();
        var isStaff = roles.Any(role => TenantWideRoles.Contains(role, StringComparer.Ordinal));

        // Staff (or a trusted system/Worker context with no roles) → tenant-wide.
        if (isStaff || !roles.Contains(CustomerRole, StringComparer.Ordinal))
            return new CallerScope(TenantWide: true, OwnerPartyId: null);

        // Customer → their own owner party, resolved from auth (never request input).
        var userId = _currentUserContext.UserId;
        if (userId is null || userId.Value == Guid.Empty)
            return new CallerScope(TenantWide: false, OwnerPartyId: null);

        var partyId = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, userId.Value, cancellationToken);
        return new CallerScope(TenantWide: false, OwnerPartyId: partyId);
    }

    /// <summary>
    /// Default classification for a document when the caller omits one (Spec 035 §10). Identity /
    /// proof images are <see cref="DocumentClassification.Sensitive"/>; explicitly tenant-wide
    /// operational content is <see cref="DocumentClassification.Internal"/>; everything else — every
    /// common personal upload and any unrecognised type — defaults to
    /// <see cref="DocumentClassification.Personal"/>, so it is owner-scoped and never indexed
    /// tenant-wide. This is the fail-closed default.
    /// </summary>
    private static DocumentClassification DefaultClassificationForType(string documentType)
    {
        var normalized = new string((documentType ?? string.Empty)
            .Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        return normalized switch
        {
            "nationalid" or "passport" or "driverslicense" or "driverslicence" or "driverlicense"
                or "idcard" or "idscan" or "identitydocument" or "proofofaddress" or "selfie" or "biometric"
                => DocumentClassification.Sensitive,
            "productterms" or "termsofservice" or "terms" or "publicnotice" or "policy" or "faq"
                => DocumentClassification.Internal,
            _ => DocumentClassification.Personal,
        };
    }

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
        DeserializeTags(d.TagsJson), d.AttributesJson, d.CreatedAt, d.UpdatedAt, d.Title);

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
