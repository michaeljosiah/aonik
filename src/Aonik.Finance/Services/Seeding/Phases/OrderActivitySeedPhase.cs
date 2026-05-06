using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Seeds ~10 demo orders (bill payments and bank transfers) so the /orders
/// list is populated after a fresh install. Orders are upserted by deterministic
/// Guid — re-running is idempotent.
/// </summary>
internal sealed class OrderActivitySeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    // Order IDs
    private static readonly Guid OrderKwameEcg         = SeedIds.OrderActivity.OrderKwameEcg;
    private static readonly Guid OrderKwameWater        = SeedIds.OrderActivity.OrderKwameWater;
    private static readonly Guid OrderTundeIkeja        = SeedIds.OrderActivity.OrderTundeIkeja;
    private static readonly Guid OrderTundeLagosWater   = SeedIds.OrderActivity.OrderTundeLagosWater;
    private static readonly Guid OrderAcmePayoutNg      = SeedIds.OrderActivity.OrderAcmePayoutNg;
    private static readonly Guid OrderAdwoaWaterFailed  = SeedIds.OrderActivity.OrderAdwoaWaterFailed;
    private static readonly Guid OrderOliviaToNaledi    = SeedIds.OrderActivity.OrderOliviaToNaledi;
    private static readonly Guid OrderLiamToKwame       = SeedIds.OrderActivity.OrderLiamToKwame;
    private static readonly Guid OrderKofiAmaTransfer   = SeedIds.OrderActivity.OrderKofiAmaTransfer;
    private static readonly Guid OrderPeterKenyaPower   = SeedIds.OrderActivity.OrderPeterKenyaPower;

    // Party IDs
    private static readonly Guid DemoPayerPartyId      = SeedIds.PartyReferences.DemoPayerPartyId;
    private static readonly Guid DemoReceiverPartyId   = SeedIds.PartyReferences.DemoReceiverPartyId;
    private static readonly Guid TundePartyIdRef       = SeedIds.PartyReferences.TundePartyId;
    private static readonly Guid AdwoaPartyIdRef       = SeedIds.PartyReferences.AdwoaPartyId;
    private static readonly Guid PeterPartyIdRef       = SeedIds.PartyReferences.PeterPartyId;
    private static readonly Guid NalediPartyIdRef      = SeedIds.PartyReferences.NalediPartyId;
    private static readonly Guid KofiPartyIdRef        = SeedIds.PartyReferences.KofiPartyId;
    private static readonly Guid AcmeImportsPartyIdRef = SeedIds.PartyReferences.AcmeImportsPartyId;
    private static readonly Guid OliviaPartyIdRef      = SeedIds.PartyReferences.OliviaPartyId;
    private static readonly Guid LiamPartyIdRef        = SeedIds.PartyReferences.LiamPartyId;

    private readonly FinanceDbContext _db;

    public OrderActivitySeedPhase(FinanceDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedContext context,
        Dictionary<string, object> results,
        CancellationToken cancellationToken)
    {
        var operations = new List<string>();
        var now = context.Now;
        var userId = context.UserId;
        var orderIds = new List<Guid>();

        var seeds = BuildOrderSeeds(context.SeedType, now);

        foreach (var seed in seeds)
        {
            var existing = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == seed.OrderId && o.TenantId == context.TenantId,
                    cancellationToken);

            if (existing is null)
            {
                existing = new Aonik.Finance.Entities.Orders.Order
                {
                    Id = seed.OrderId,
                    TenantId = context.TenantId,
                    OrderType = seed.OrderType,
                    PayerPartyId = seed.PayerPartyId,
                    PurposeCode = seed.PurposeCode,
                    OriginCountry = seed.OriginCountry,
                    DestinationCountry = seed.DestinationCountry,
                    AmountIn = seed.AmountIn,
                    CurrencyIn = seed.CurrencyIn,
                    AmountOut = seed.AmountOut,
                    CurrencyOut = seed.CurrencyOut,
                    FeesJson = "[]",
                    Status = seed.Status,
                    ProvenanceJson = "{\"source\":\"demo-seed\"}",
                    CreatedAt = seed.CreatedAt,
                    CreatedBy = userId,
                };
                _db.Orders.Add(existing);
                operations.Add($"Seeded order {seed.OrderId:D}");
            }
            else
            {
                existing.OrderType = seed.OrderType;
                existing.Status = seed.Status;
                existing.AmountIn = seed.AmountIn;
                existing.CurrencyIn = seed.CurrencyIn;
                existing.AmountOut = seed.AmountOut;
                existing.CurrencyOut = seed.CurrencyOut;
                existing.UpdatedAt = now;
                existing.UpdatedBy = userId;
            }

            orderIds.Add(seed.OrderId);

            // Replace items + party roles each run — hard-delete via
            // ExecuteDeleteAsync so the audit hook doesn't soft-delete
            // them and leave ghost rows on the next re-seed.
            await _db.OrderItems
                .IncludeSoftDeleted()
                .Where(i => i.OrderId == seed.OrderId)
                .ExecuteDeleteAsync(cancellationToken);

            for (var idx = 0; idx < seed.Items.Count; idx++)
            {
                var item = seed.Items[idx];
                _db.OrderItems.Add(new Aonik.Finance.Entities.Orders.OrderItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    OrderId = seed.OrderId,
                    ItemType = item.ItemType,
                    ItemIndex = idx,
                    DetailsJson = item.DetailsJson,
                    Status = seed.Status,
                    ReceiverPartyId = item.ReceiverPartyId,
                    AmountIn = item.AmountIn,
                    CurrencyIn = item.CurrencyIn,
                    AmountOut = item.AmountOut,
                    CurrencyOut = item.CurrencyOut,
                    FeesTotal = item.FeesTotal,
                    CreatedAt = seed.CreatedAt,
                    CreatedBy = userId,
                });
            }

            await _db.OrderPartyRoles
                .IncludeSoftDeleted()
                .Where(r => r.OrderId == seed.OrderId)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var role in seed.PartyRoles)
            {
                _db.OrderPartyRoles.Add(new Aonik.Finance.Entities.Orders.OrderPartyRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    OrderId = seed.OrderId,
                    PartyId = role.PartyId,
                    Role = role.Role,
                    CreatedAt = seed.CreatedAt,
                    CreatedBy = userId,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        results[DemoSeedResultKeys.OrderIds] = orderIds.ToArray();
        return operations;
    }

    private static IReadOnlyList<DemoOrderSeed> BuildOrderSeeds(string seedType, DateTime now)
    {
        var seeds = new List<DemoOrderSeed>
        {
            BillPay(OrderKwameEcg,         DemoPayerPartyId,  "GH", 250m,  "GHS", "ECG",          OrderStatuses.Pending,     now.AddDays(-1)),
            BillPay(OrderKwameWater,       DemoPayerPartyId,  "GH",  90m,  "GHS", "GhanaWater",   OrderStatuses.Complete,    now.AddDays(-3)),
            BillPay(OrderTundeIkeja,       TundePartyIdRef,   "NG", 8500m, "NGN", "IkejaElectric", OrderStatuses.Pending,    now.AddHours(-6)),
            BillPay(OrderTundeLagosWater,  TundePartyIdRef,   "NG", 4200m, "NGN", "LagosWater",   OrderStatuses.Transmitted, now.AddDays(-2)),
            BillPay(OrderAdwoaWaterFailed, AdwoaPartyIdRef,   "GH",  60m,  "GHS", "GhanaWater",   OrderStatuses.Failed,      now.AddDays(-9),
                detailsExtra: ",\"failureReason\":\"insufficient_funds\""),
            Transfer(OrderAcmePayoutNg, AcmeImportsPartyIdRef, PeterPartyIdRef, "GB", "NG", 2500m, "GBP", 4_902_500m, "NGN", 8.50m,
                "supplier_payment", OrderStatuses.Complete, now.AddDays(-5)),
            Transfer(OrderKofiAmaTransfer, KofiPartyIdRef, DemoReceiverPartyId, "GH", "GH", 200m, "GHS", 200m, "GHS", 1.50m,
                "family_support", OrderStatuses.Complete, now.AddDays(-7)),
        };

        if (string.Equals(seedType, "CrossBorderPayments", StringComparison.OrdinalIgnoreCase))
        {
            seeds.Add(Transfer(OrderOliviaToNaledi, OliviaPartyIdRef, NalediPartyIdRef, "GB", "ZA", 1500m, "GBP", 35_550m, "ZAR", 6.50m,
                "remittance", OrderStatuses.Complete, now.AddDays(-4)));
            seeds.Add(Transfer(OrderLiamToKwame, LiamPartyIdRef, DemoPayerPartyId, "GB", "GH", 750m, "GBP", 11_400m, "GHS", 4.50m,
                "remittance", OrderStatuses.Pending, now.AddHours(-12)));
            seeds.Add(BillPay(OrderPeterKenyaPower, PeterPartyIdRef, "KE", 1200m, "KES", "KenyaPower", OrderStatuses.Complete, now.AddDays(-8)));
        }

        return seeds;
    }

    private static DemoOrderSeed BillPay(
        Guid orderId,
        Guid payerPartyId,
        string country,
        decimal amount,
        string currency,
        string biller,
        string status,
        DateTime createdAt,
        string detailsExtra = "")
    {
        var details = $"{{\"biller\":\"{biller}\"{detailsExtra}}}";
        return new DemoOrderSeed(
            OrderId: orderId,
            OrderType: "BillPayment",
            PayerPartyId: payerPartyId,
            PurposeCode: "BillPayment",
            OriginCountry: country,
            DestinationCountry: country,
            AmountIn: amount,
            CurrencyIn: currency,
            AmountOut: amount,
            CurrencyOut: currency,
            Status: status,
            CreatedAt: createdAt,
            Items: new[] { new DemoOrderItemSeed("BillPayment", payerPartyId, amount, currency, amount, currency, 0m, details) },
            PartyRoles: new[]
            {
                new DemoOrderRoleSeed(payerPartyId, "Payer"),
                new DemoOrderRoleSeed(payerPartyId, "Payee"),
            });
    }

    private static DemoOrderSeed Transfer(
        Guid orderId,
        Guid payerPartyId,
        Guid receiverPartyId,
        string originCountry,
        string destinationCountry,
        decimal amountIn,
        string currencyIn,
        decimal amountOut,
        string currencyOut,
        decimal feesTotal,
        string purpose,
        string status,
        DateTime createdAt)
    {
        var details = $"{{\"purpose\":\"{purpose}\",\"corridor\":\"{originCountry}-{destinationCountry}\"}}";
        return new DemoOrderSeed(
            OrderId: orderId,
            OrderType: "BankTransfer",
            PayerPartyId: payerPartyId,
            PurposeCode: purpose,
            OriginCountry: originCountry,
            DestinationCountry: destinationCountry,
            AmountIn: amountIn,
            CurrencyIn: currencyIn,
            AmountOut: amountOut,
            CurrencyOut: currencyOut,
            Status: status,
            CreatedAt: createdAt,
            Items: new[] { new DemoOrderItemSeed("BankTransfer", receiverPartyId, amountIn, currencyIn, amountOut, currencyOut, feesTotal, details) },
            PartyRoles: new[]
            {
                new DemoOrderRoleSeed(payerPartyId, "Payer"),
                new DemoOrderRoleSeed(receiverPartyId, "Receiver"),
            });
    }

    private sealed record DemoOrderSeed(
        Guid OrderId,
        string OrderType,
        Guid PayerPartyId,
        string PurposeCode,
        string OriginCountry,
        string DestinationCountry,
        decimal AmountIn,
        string CurrencyIn,
        decimal AmountOut,
        string CurrencyOut,
        string Status,
        DateTime CreatedAt,
        IReadOnlyList<DemoOrderItemSeed> Items,
        IReadOnlyList<DemoOrderRoleSeed> PartyRoles);

    private sealed record DemoOrderItemSeed(
        string ItemType,
        Guid ReceiverPartyId,
        decimal AmountIn,
        string CurrencyIn,
        decimal AmountOut,
        string CurrencyOut,
        decimal FeesTotal,
        string DetailsJson);

    private sealed record DemoOrderRoleSeed(Guid PartyId, string Role);
}
