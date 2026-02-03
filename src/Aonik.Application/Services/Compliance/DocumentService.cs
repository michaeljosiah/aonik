using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Compliance;
using Aonik.Domain.Compliance.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Compliance;

public class DocumentService : IDocumentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public DocumentService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<DocumentResponse> CreateDocumentAsync(
        CreateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            throw new ArgumentException("Document type is required.", nameof(request.DocumentType));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OwnerPartyId = request.OwnerPartyId,
            DocumentType = request.DocumentType.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim(),
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            IssuerName = request.IssuerName?.Trim(),
            CountryCode = request.CountryCode?.Trim(),
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            TagsJson = SerializeTags(request.Tags),
            AttributesJson = string.IsNullOrWhiteSpace(request.AttributesJson) ? "{}" : request.AttributesJson
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapDocument(document);
    }

    public async Task<DocumentFileResponse> AddDocumentFileAsync(
        AddDocumentFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StorageProvider))
        {
            throw new ArgumentException("Storage provider is required.", nameof(request.StorageProvider));
        }

        if (string.IsNullOrWhiteSpace(request.StorageKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(request.StorageKey));
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new ArgumentException("Content type is required.", nameof(request.ContentType));
        }

        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document == null)
        {
            throw new InvalidOperationException($"Document {request.DocumentId} not found.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var file = new DocumentFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = document.Id,
            StorageProvider = request.StorageProvider.Trim(),
            StorageContainer = request.StorageContainer?.Trim(),
            StorageKey = request.StorageKey.Trim(),
            ContentType = request.ContentType.Trim(),
            FileName = request.FileName?.Trim(),
            FileSizeBytes = request.FileSizeBytes,
            Sha256 = request.Sha256?.Trim(),
            PageIndex = request.PageIndex,
            Side = request.Side?.Trim(),
            CapturedAt = request.CapturedAt,
            CapturedBy = request.CapturedBy?.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? "{}" : request.MetadataJson
        };

        _dbContext.DocumentFiles.Add(file);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapFile(file);
    }

    public async Task<DocumentUsageResponse> AddDocumentUsageAsync(
        AddDocumentUsageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Purpose))
        {
            throw new ArgumentException("Purpose is required.", nameof(request.Purpose));
        }

        var documentExists = await _dbContext.Documents
            .AsNoTracking()
            .AnyAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (!documentExists)
        {
            throw new InvalidOperationException($"Document {request.DocumentId} not found.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var usage = new DocumentUsage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = request.DocumentId,
            OwnerPartyId = request.OwnerPartyId,
            Purpose = request.Purpose.Trim(),
            RelatedEntityType = request.RelatedEntityType?.Trim(),
            RelatedEntityId = request.RelatedEntityId,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status.Trim(),
            Notes = request.Notes?.Trim()
        };

        _dbContext.DocumentUsages.Add(usage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapUsage(usage, Array.Empty<DocumentVerification>());
    }

    public async Task<DocumentVerificationResponse> AddDocumentVerificationAsync(
        AddDocumentVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Decision))
        {
            throw new ArgumentException("Decision is required.", nameof(request.Decision));
        }

        if (string.IsNullOrWhiteSpace(request.VerifierType))
        {
            throw new ArgumentException("Verifier type is required.", nameof(request.VerifierType));
        }

        var usage = await _dbContext.DocumentUsages
            .FirstOrDefaultAsync(u => u.Id == request.DocumentUsageId, cancellationToken);

        if (usage == null)
        {
            throw new InvalidOperationException($"Document usage {request.DocumentUsageId} not found.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var verification = new DocumentVerification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentUsageId = usage.Id,
            Decision = request.Decision.Trim(),
            DecisionReasonCode = request.DecisionReasonCode?.Trim(),
            DecisionNotes = request.DecisionNotes?.Trim(),
            VerifierType = request.VerifierType.Trim(),
            VerifierId = request.VerifierId?.Trim(),
            AiRunId = request.AiRunId
        };

        usage.VerifiedAt = _clock.UtcNow;
        usage.Status = NormalizeUsageStatus(verification.Decision, usage.Status);

        _dbContext.DocumentVerifications.Add(verification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapVerification(verification);
    }

    public async Task<DocumentDetailsResponse?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.Files)
            .Include(d => d.Usages)
            .ThenInclude(u => u.Verifications)
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            return null;
        }

        var files = document.Files.Select(MapFile).ToList();
        var usages = document.Usages.Select(usage =>
            MapUsage(usage, usage.Verifications)).ToList();
        var versions = document.Versions.Select(MapVersion).ToList();

        return new DocumentDetailsResponse(
            MapDocument(document),
            files,
            usages,
            versions);
    }

    private static string SerializeTags(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return "[]";
        }

        var normalized = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static IReadOnlyList<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions);
            return tags ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string NormalizeUsageStatus(string decision, string currentStatus)
    {
        if (decision.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            return "Satisfied";
        }

        if (decision.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return "Rejected";
        }

        return currentStatus;
    }

    private static DocumentResponse MapDocument(Document document)
    {
        return new DocumentResponse(
            document.Id,
            document.OwnerPartyId,
            document.DocumentType,
            document.Status,
            document.IssuedOn,
            document.ExpiresOn,
            document.IssuerName,
            document.CountryCode,
            document.ReferenceNumber,
            DeserializeTags(document.TagsJson),
            document.AttributesJson,
            document.CreatedAt,
            document.UpdatedAt);
    }

    private static DocumentFileResponse MapFile(DocumentFile file)
    {
        return new DocumentFileResponse(
            file.Id,
            file.DocumentId,
            file.StorageProvider,
            file.StorageContainer,
            file.StorageKey,
            file.ContentType,
            file.FileName,
            file.FileSizeBytes,
            file.Sha256,
            file.PageIndex,
            file.Side,
            file.CapturedAt,
            file.CapturedBy,
            file.MetadataJson,
            file.CreatedAt);
    }

    private static DocumentUsageResponse MapUsage(
        DocumentUsage usage,
        IReadOnlyList<DocumentVerification> verifications)
    {
        var mappedVerifications = verifications.Select(MapVerification).ToList();
        return new DocumentUsageResponse(
            usage.Id,
            usage.DocumentId,
            usage.OwnerPartyId,
            usage.Purpose,
            usage.RelatedEntityType,
            usage.RelatedEntityId,
            usage.Status,
            usage.VerifiedByUserId,
            usage.VerifiedAt,
            usage.Notes,
            mappedVerifications,
            usage.CreatedAt,
            usage.UpdatedAt);
    }

    private static DocumentVerificationResponse MapVerification(DocumentVerification verification)
    {
        return new DocumentVerificationResponse(
            verification.Id,
            verification.DocumentUsageId,
            verification.Decision,
            verification.DecisionReasonCode,
            verification.DecisionNotes,
            verification.VerifierType,
            verification.VerifierId,
            verification.AiRunId,
            verification.CreatedAt);
    }

    private static DocumentVersionResponse MapVersion(DocumentVersion version)
    {
        return new DocumentVersionResponse(
            version.Id,
            version.DocumentId,
            version.Version,
            version.Status,
            version.SubmittedAt,
            version.DecisionedAt,
            version.DecisionReason,
            version.CreatedAt,
            version.UpdatedAt);
    }
}
