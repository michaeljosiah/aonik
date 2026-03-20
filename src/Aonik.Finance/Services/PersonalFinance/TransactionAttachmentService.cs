using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class TransactionAttachmentService : ITransactionAttachmentService
{
    private readonly FinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFileStore _fileStore;

    public TransactionAttachmentService(
        FinanceDbContext db,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFileStore fileStore)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _fileStore = fileStore;
    }

    public async Task<IReadOnlyList<TransactionAttachmentResponse>> GetAttachmentsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        var attachments = await _db.TransactionAttachments
            .AsNoTracking()
            .Where(a => a.TransactionId == transactionId && a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return attachments.Select(MapToResponse).ToList();
    }

    public async Task<TransactionAttachmentResponse> AddAttachmentAsync(
        Guid transactionId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Verify the transaction exists and belongs to this user
        var transactionExists = await _db.PersonalTransactions
            .AnyAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (!transactionExists)
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found.");
        }

        // Upload to blob storage
        var uploadResult = await _fileStore.UploadAsync(
            tenantId,
            transactionId,
            fileStream,
            fileName,
            contentType,
            cancellationToken);

        var attachment = new TransactionAttachment
        {
            TenantId = tenantId,
            UserId = userId,
            TransactionId = transactionId,
            StorageProvider = uploadResult.StorageProvider,
            StorageContainer = uploadResult.StorageContainer,
            StorageKey = uploadResult.StorageKey,
            ContentType = uploadResult.ContentType,
            FileName = uploadResult.FileName,
            FileSizeBytes = uploadResult.FileSizeBytes,
            Sha256 = uploadResult.Sha256
        };

        _db.TransactionAttachments.Add(attachment);
        await _db.SaveChangesAsync(cancellationToken);

        return MapToResponse(attachment);
    }

    public async Task DeleteAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        var attachment = await _db.TransactionAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.UserId == userId, cancellationToken);

        if (attachment == null)
        {
            throw new InvalidOperationException($"Attachment {attachmentId} not found.");
        }

        // Delete from blob storage (best-effort; entity deletion is authoritative)
        try
        {
            await _fileStore.DeleteAsync(attachment.StorageKey, cancellationToken);
        }
        catch
        {
            // Log but don't fail — the soft-delete on the entity is what matters
        }

        _db.TransactionAttachments.Remove(attachment);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ── Private helpers ──────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (userId == null || userId == Guid.Empty)
        {
            throw new InvalidOperationException("Current user could not be determined.");
        }
        return userId.Value;
    }

    private TransactionAttachmentResponse MapToResponse(TransactionAttachment a)
    {
        var url = _fileStore.GetUrl(a.StorageKey);

        // Generate thumbnail URL for images (future: dedicated thumbnail pipeline)
        string? thumbnailUrl = a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? url
            : null;

        return new TransactionAttachmentResponse(
            a.Id,
            a.FileName,
            a.ContentType,
            url,
            thumbnailUrl,
            a.FileSizeBytes,
            a.CreatedAt);
    }
}
