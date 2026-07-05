using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services.Accounts.Linking;

/// <summary>
/// Persists, lists, and deletes transaction attachments. Coordinates the
/// blob upload via <see cref="IFileStore"/> with the corresponding
/// <see cref="AccountTransactionAttachment"/> row in the finance database.
/// </summary>
internal sealed class TransactionAttachmentHandler
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IFileStore _fileStore;

    public TransactionAttachmentHandler(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        IFileStore fileStore)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _fileStore = fileStore;
    }

    public async Task<AccountTransactionAttachmentResponse> AddAsync(
        Guid transactionId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var transactionExists = await _financeDbContext.AccountTransactions
            .AnyAsync(t => t.Id == transactionId && t.TenantId == tenantId, cancellationToken);

        if (!transactionExists)
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found.");
        }

        var uploadResult = await _fileStore.UploadAsync(
            tenantId,
            transactionId,
            fileStream,
            fileName,
            contentType,
            cancellationToken);

        var attachment = new AccountTransactionAttachment
        {
            TenantId = tenantId,
            TransactionId = transactionId,
            StorageProvider = uploadResult.StorageProvider,
            StorageContainer = uploadResult.StorageContainer,
            StorageKey = uploadResult.StorageKey,
            ContentType = uploadResult.ContentType,
            FileName = uploadResult.FileName,
            FileSizeBytes = uploadResult.FileSizeBytes,
            Sha256 = uploadResult.Sha256
        };

        _financeDbContext.AccountTransactionAttachments.Add(attachment);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapAttachment(attachment);
    }

    public async Task<IReadOnlyList<AccountTransactionAttachmentResponse>> ListAsync(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var attachments = await _financeDbContext.AccountTransactionAttachments
            .AsNoTracking()
            .Where(a => a.TransactionId == transactionId && a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return attachments.Select(MapAttachment).ToList();
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var attachment = await _financeDbContext.AccountTransactionAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TenantId == tenantId, cancellationToken);

        if (attachment == null)
        {
            throw new InvalidOperationException($"Attachment {attachmentId} not found.");
        }

        try
        {
            await _fileStore.DeleteAsync(attachment.StorageKey, cancellationToken);
        }
        catch
        {
            // Best-effort blob deletion; entity removal is authoritative
        }

        _financeDbContext.AccountTransactionAttachments.Remove(attachment);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
    }

    private AccountTransactionAttachmentResponse MapAttachment(AccountTransactionAttachment attachment)
    {
        return new AccountTransactionAttachmentResponse(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            _fileStore.GetUrl(attachment.StorageKey),
            attachment.FileSizeBytes,
            attachment.CreatedAt);
    }
}
