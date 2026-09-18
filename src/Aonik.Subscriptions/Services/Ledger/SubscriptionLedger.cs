using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Modules;

namespace Aonik.Subscriptions.Services.Ledger;

/// <summary>
/// The accounts this module posts to (Spec 087 §13).
///
/// Deliberately outside the 2000-2200 band already in use: 2100 is Payments Clearing and 2200 is
/// Customer Funds Liability from the remittance flow, so a new prepaid liability needed its own
/// code rather than sharing one.
/// </summary>
public static class SubscriptionAccounts
{
    /// <summary>Units bought and not yet consumed — <b>the prepaid balance, as a liability</b>, not revenue on receipt.</summary>
    public const string DeferredEntitlements = "2210";

    /// <summary>Plan fees.</summary>
    public const string SubscriptionRevenue = "4100";

    /// <summary>Recognised as purchased units are actually consumed.</summary>
    public const string EntitlementRevenue = "4110";

    /// <summary>What a metered run cost us. Margin per meter is 4110 minus this, on the meter dimension.</summary>
    public const string ProviderCost = "5100";

    /// <summary>Where the provider cost is owed. Reuses the existing Accounts Payable.</summary>
    public const string AccountsPayable = "2000";
}

/// <summary>Declares those accounts so Finance creates them at provisioning (Spec 088 §5).</summary>
internal sealed class SubscriptionLedgerAccountContributor : ILedgerAccountContributor
{
    public string ModuleName => ModuleIds.Subscriptions;

    public IReadOnlyCollection<LedgerAccountDefinition> GetAccounts() =>
    [
        new(SubscriptionAccounts.DeferredEntitlements, "Deferred Revenue - Purchased Entitlements", "Liability"),
        new(SubscriptionAccounts.SubscriptionRevenue, "Subscription Revenue", "Revenue"),
        new(SubscriptionAccounts.EntitlementRevenue, "Entitlement Revenue", "Revenue"),
        new(SubscriptionAccounts.ProviderCost, "Cost of Service - Providers", "Expense")
    ];
}

/// <summary>
/// Routes settled subscription invoices (Spec 087 §13.1) into the seam Spec 088 §9 provides.
///
/// Two order types, two very different destinations: a plan fee is <b>earned</b> when the period is
/// served, so it credits revenue; purchased units are <b>owed</b> until consumed, so they credit a
/// liability. Treating the second as revenue on receipt would overstate earnings and understate
/// what the business still owes its customers.
/// </summary>
internal sealed class SubscriptionSettlementRevenueResolver : ISettlementRevenueResolver
{
    public IReadOnlyCollection<string> OrderTypes =>
    [
        OrderTypeCodes.SubscriptionRenewal,
        OrderTypeCodes.EntitlementPurchase
    ];

    public SettlementCredit Resolve(string orderType, SettlementLineContext line)
    {
        if (string.Equals(orderType, OrderTypeCodes.EntitlementPurchase, StringComparison.OrdinalIgnoreCase))
        {
            return new SettlementCredit(
                SubscriptionAccounts.DeferredEntitlements,
                "Deferred Revenue - Purchased Entitlements",
                "Liability",
                MeterDimension(line));
        }

        return new SettlementCredit(
            SubscriptionAccounts.SubscriptionRevenue,
            "Subscription Revenue",
            "Revenue",
            MeterDimension(line));
    }

    /// <summary>
    /// The meter this line is for, read from the line's own metadata. An invoice line carries no
    /// link back to the order line that produced it, so matching on description or position would
    /// be guesswork — this module wrote the metadata and is the only thing that can read it.
    /// </summary>
    private static string? MeterDimension(SettlementLineContext line)
    {
        var meterCode = ReadMeterCode(line.MetadataJson);
        return meterCode is null ? null : JsonSerializer.Serialize(new { meterCode });
    }

    internal static string? ReadMeterCode(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.TryGetProperty("meterCode", out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            // Metadata is a free-form blob other code may also write to. Unreadable content means
            // an undimensioned line, never a failed settlement — refusing to settle real money
            // because a tag could not be parsed would be the wrong trade.
            return null;
        }
    }
}
