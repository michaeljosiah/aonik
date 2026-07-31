using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Ledger;

/// <summary>Spec 088 §5.1 — resolves the tenant's canonical ledger, or refuses.</summary>
internal sealed class LedgerResolver : ILedgerResolver
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public LedgerResolver(FinanceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<Guid> GetCanonicalLedgerIdAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var marked = await _dbContext.Ledgers.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.IsCanonical)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (marked is not null)
            return marked.Value;

        var ids = await _dbContext.Ledgers.AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        return ids.Count switch
        {
            1 => ids[0],
            0 => throw new InvalidStateException(
                $"Tenant '{tenantId}' has no ledger. Provision one before posting."),

            // Deliberately a refusal, not a choice. Which ledger is canonical is an operator's
            // decision; inventing one here would post material entries into an unpredictable
            // ledger with an unpredictable base currency, and the error would compound silently.
            _ => throw new InvalidStateException(
                $"Tenant '{tenantId}' has more than one ledger and none is marked canonical. "
                + "Mark one before posting through ILedgerResolver, or name the ledger explicitly.")
        };
    }
}
