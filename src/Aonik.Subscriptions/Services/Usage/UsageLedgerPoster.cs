using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Usage;
using Aonik.Subscriptions.Services.Ledger;

namespace Aonik.Subscriptions.Services.Usage;

/// <summary>
/// Spec 087 §13 — turns committed usage into ledger entries.
///
/// Two things happen when metered work is consumed, and they are separate entries because they
/// answer separate questions:
/// <list type="bullet">
///   <item>Purchased units stop being owed and become earned: Dr 2210 / Cr 4110.</item>
///   <item>The run cost us something: Dr 5100 / Cr 2000.</item>
/// </list>
/// Both carry the meter dimension, which is what makes <b>margin per meter</b> — 4110 minus 5100
/// on that dimension — readable from the ledger alone rather than from a spreadsheet.
/// </summary>
internal sealed class UsageLedgerPoster
{
    private const string ConsumptionSourceType = "EntitlementConsumption";
    private const string ProviderCostSourceType = "EntitlementProviderCost";

    private readonly IJournalWriter _journalWriter;
    private readonly ILedgerResolver _ledgerResolver;

    public UsageLedgerPoster(IJournalWriter journalWriter, ILedgerResolver ledgerResolver)
    {
        _journalWriter = journalWriter;
        _ledgerResolver = ledgerResolver;
    }

    public async Task PostConsumptionAsync(
        UsageRecord record,
        IReadOnlyList<GrantAllocation> allocations,
        CancellationToken cancellationToken = default)
    {
        // Only PURCHASED units carry a liability. Plan allowance was paid for as access, not as
        // units, so consuming it recognises nothing — the subscription fee was already revenue.
        var purchased = allocations
            .Where(a => string.Equals(a.Source, GrantSources.Purchase, StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.Quantity);

        var dimensions = JsonSerializer.Serialize(new { meterCode = record.MeterCode });

        if (purchased > 0)
        {
            var unitValue = await ResolveUnitValueAsync(record, cancellationToken);
            var amount = purchased * unitValue;

            if (amount > 0)
            {
                var ledgerId = await _ledgerResolver.GetCanonicalLedgerIdAsync(cancellationToken);

                await _journalWriter.PostAsync(new PostJournalCommand(
                    ledgerId,
                    ConsumptionSourceType,
                    // Keyed on the usage record, so a retried commit returns the existing entry
                    // rather than recognising the same revenue twice.
                    record.Id,
                    [
                        new JournalLineSpec(SubscriptionAccounts.DeferredEntitlements, JournalDirections.Debit,
                            amount, record.ProviderCostCurrency ?? "GBP", "Entitlement consumed", dimensions),
                        new JournalLineSpec(SubscriptionAccounts.EntitlementRevenue, JournalDirections.Credit,
                            amount, record.ProviderCostCurrency ?? "GBP", "Entitlement revenue", dimensions)
                    ]),
                    cancellationToken);
            }
        }

        if (record.ProviderCost is > 0 && record.ProviderCostCurrency is { } currency)
        {
            var ledgerId = await _ledgerResolver.GetCanonicalLedgerIdAsync(cancellationToken);

            await _journalWriter.PostAsync(new PostJournalCommand(
                ledgerId,
                ProviderCostSourceType,
                record.Id,
                [
                    new JournalLineSpec(SubscriptionAccounts.ProviderCost, JournalDirections.Debit,
                        record.ProviderCost.Value, currency, "Provider cost", dimensions),
                    new JournalLineSpec(SubscriptionAccounts.AccountsPayable, JournalDirections.Credit,
                        record.ProviderCost.Value, currency, "Provider cost payable", dimensions)
                ]),
                cancellationToken);
        }
    }

    /// <summary>
    /// What one consumed unit is worth, for the revenue-recognition leg.
    /// </summary>
    /// <remarks>
    /// P5 has no priced offer to read — <c>MeterOffer</c> arrives in P6 — so this uses the recorded
    /// provider cost per unit as a stand-in when nothing better exists, and posts nothing when
    /// there is no basis at all. Recognising a made-up amount would be worse than recognising
    /// late: the correction would have to be a compensating entry against real customer revenue.
    /// See Spec 087 O7.
    /// </remarks>
    private Task<decimal> ResolveUnitValueAsync(UsageRecord record, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (record.ProviderCost is > 0 && record.Quantity > 0)
            return Task.FromResult(record.ProviderCost.Value / record.Quantity);

        return Task.FromResult(0m);
    }
}
