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

        // Spec 087 O7 - recognise at the price the customer was actually charged, recorded on the
        // grant from its offer. An estimate here would need a compensating entry against real
        // customer revenue to correct.
        var recognisable = allocations
            .Where(a => string.Equals(a.Source, GrantSources.Purchase, StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.Quantity * (a.UnitValue ?? 0m));

        var currency = allocations.FirstOrDefault(a => a.UnitValueCurrency is not null)?.UnitValueCurrency;

        if (purchased > 0 && currency is not null)
        {
            var amount = recognisable;

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
                            amount, currency, "Entitlement consumed", dimensions),
                        new JournalLineSpec(SubscriptionAccounts.EntitlementRevenue, JournalDirections.Credit,
                            amount, currency, "Entitlement revenue", dimensions)
                    ]),
                    cancellationToken);
            }
        }

        if (record.ProviderCost is > 0 && record.ProviderCostCurrency is { } costCurrency)
        {
            var ledgerId = await _ledgerResolver.GetCanonicalLedgerIdAsync(cancellationToken);

            await _journalWriter.PostAsync(new PostJournalCommand(
                ledgerId,
                ProviderCostSourceType,
                record.Id,
                [
                    new JournalLineSpec(SubscriptionAccounts.ProviderCost, JournalDirections.Debit,
                        record.ProviderCost.Value, costCurrency, "Provider cost", dimensions),
                    new JournalLineSpec(SubscriptionAccounts.AccountsPayable, JournalDirections.Credit,
                        record.ProviderCost.Value, costCurrency, "Provider cost payable", dimensions)
                ]),
                cancellationToken);
        }
    }
}
