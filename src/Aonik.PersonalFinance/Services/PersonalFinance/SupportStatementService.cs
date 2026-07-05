using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Composes the Support Statement projection (Spec 048 §9) for the caller's own
/// CareEntity — per-currency totals (never converted), a corroboration column,
/// the receipt appendix, and a verification code. The PDF render is client-side.
/// </summary>
internal sealed class SupportStatementService : ISupportStatementService
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IDocumentLinkReader _documentLinkReader;

    public SupportStatementService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IDocumentLinkReader documentLinkReader)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _documentLinkReader = documentLinkReader;
    }

    public async Task<StatementData?> ComposeAsync(
        Guid careEntityId,
        DateTime from,
        DateTime to,
        string? preparedFor,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var entity = await _dbContext.CareEntities.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == careEntityId && e.TenantId == tenantId && e.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var fromDate = from.Date;
        var toDate = to.Date;

        var logs = await _dbContext.PaymentLogs.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId && p.CareEntityId == careEntityId
                && p.Date >= fromDate && p.Date <= toDate)
            .OrderBy(p => p.Date)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var rows = logs
            .Select(p => new StatementRow(
                p.Date, p.Note, p.Channel, p.Amount, p.Currency,
                ReceiptDocumentId: null, // per-row receipt linkage is a follow-up; appendix lists entity docs
                Corroborated: p.CorroborationStatus == "confirmed"))
            .ToList();

        var totals = logs
            .GroupBy(p => p.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(p => p.Amount), g.Count()))
            .OrderBy(t => t.Currency)
            .ToList();

        // The caller is the owner, so the owner-scoped link reader returns their docs.
        var docRefs = await _documentLinkReader.GetForTargetAsync("careEntity", careEntityId, cancellationToken);
        var appendix = docRefs
            .Select(d => new CareEntityDocumentRef(d.DocumentId, d.Title, d.DocumentType))
            .ToList();

        var verificationCode = $"SIMI-{careEntityId:N}"[..13].ToUpperInvariant() + $"-{fromDate:yyyyMMdd}";

        return new StatementData(
            Entity: new CareEntityRef(entity.Id, entity.Name, entity.Kind, entity.CountryCode),
            From: fromDate,
            To: toDate,
            PreparedFor: string.IsNullOrWhiteSpace(preparedFor) ? null : preparedFor.Trim(),
            Rows: rows,
            Totals: totals,
            ReceiptAppendix: appendix,
            VerificationCode: verificationCode);
    }
}
