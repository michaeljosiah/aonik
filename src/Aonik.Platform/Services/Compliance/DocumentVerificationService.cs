using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Compliance;

/// <summary>
/// Compliance verification half of the former <c>DocumentService</c> (Spec 035 §12). Owns
/// DocumentUsage / DocumentVerification (which stay in Aonik.Platform) and resolves the referenced
/// document by id through <see cref="IDocumentReader"/> — no EF navigation to the moved Document entity.
/// </summary>
internal sealed class DocumentVerificationService : IDocumentVerificationService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IDocumentReader _documentReader;

    public DocumentVerificationService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IDocumentReader documentReader)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _documentReader = documentReader;
    }

    public async Task<DocumentUsageResponse> AddDocumentUsageAsync(
        AddDocumentUsageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Purpose))
            throw new ArgumentException("Purpose is required.", nameof(request));

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Resolve the document across the module boundary; no FK navigation.
        var document = await _documentReader.GetDocumentAsync(request.DocumentId, cancellationToken);
        if (document is null)
            throw new InvalidOperationException($"Document {request.DocumentId} not found.");

        var usage = new DocumentUsage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = request.DocumentId,
            OwnerPartyId = request.OwnerPartyId,
            Purpose = request.Purpose.Trim(),
            RelatedEntityType = request.RelatedEntityType?.Trim(),
            RelatedEntityId = request.RelatedEntityId,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status!.Trim(),
            Notes = request.Notes?.Trim(),
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
            throw new ArgumentException("Decision is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.VerifierType))
            throw new ArgumentException("Verifier type is required.", nameof(request));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var usage = await _dbContext.DocumentUsages
            .FirstOrDefaultAsync(u => u.Id == request.DocumentUsageId && u.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Document usage {request.DocumentUsageId} not found.");

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
            AiRunId = request.AiRunId,
        };

        if (IsFinalDecision(verification.Decision))
            usage.VerifiedAt = _clock.UtcNow;
        usage.Status = NormalizeUsageStatus(verification.Decision, usage.Status);

        _dbContext.DocumentVerifications.Add(verification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapVerification(verification);
    }

    private static string NormalizeUsageStatus(string decision, string currentStatus)
        => decision.Equals("Approved", StringComparison.OrdinalIgnoreCase) ? "Satisfied"
        : decision.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ? "Rejected"
        : currentStatus;

    private static bool IsFinalDecision(string decision)
        => decision.Equals("Approved", StringComparison.OrdinalIgnoreCase)
        || decision.Equals("Rejected", StringComparison.OrdinalIgnoreCase);

    private static DocumentUsageResponse MapUsage(
        DocumentUsage usage,
        IReadOnlyList<DocumentVerification> verifications)
        => new(
            usage.Id, usage.DocumentId, usage.OwnerPartyId, usage.Purpose, usage.RelatedEntityType,
            usage.RelatedEntityId, usage.Status, usage.VerifiedByUserId, usage.VerifiedAt, usage.Notes,
            verifications.Select(MapVerification).ToList(), usage.CreatedAt, usage.UpdatedAt);

    private static DocumentVerificationResponse MapVerification(DocumentVerification v)
        => new(
            v.Id, v.DocumentUsageId, v.Decision, v.DecisionReasonCode, v.DecisionNotes,
            v.VerifierType, v.VerifierId, v.AiRunId, v.CreatedAt);
}
