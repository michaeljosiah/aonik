using System.Globalization;

using Aonik.Finance.Contracts.Api.PayActivity;
using Aonik.Finance.Contracts.Services.PayActivity;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PayActivity;

internal sealed class PayActivityService : IPayActivityService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PayActivityService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PayActivitySummaryResponse> GetRecentActivityAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var partyId = await ResolvePartyIdAsync(userId, tenantId, cancellationToken);
        if (partyId == null)
        {
            return new PayActivitySummaryResponse(Array.Empty<PayActivityTransactionDto>());
        }

        // Query orders where the user is the payer, most recent first.
        // Include items for receiver/description info and join with payment intents.
        var orders = await _financeDbContext.Orders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.PayerPartyId == partyId.Value)
            .OrderByDescending(o => o.CreatedAt)
            .Take(20)
            .Select(o => new
            {
                o.Id,
                o.OrderType,
                o.Status,
                o.AmountIn,
                o.CurrencyIn,
                o.CreatedAt,
                FirstItem = o.Items
                    .OrderBy(i => i.ItemIndex)
                    .Select(i => new { i.ReceiverPartyId })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return new PayActivitySummaryResponse(Array.Empty<PayActivityTransactionDto>());
        }

        // Resolve receiver party names for display titles
        var receiverPartyIds = orders
            .Where(o => o.FirstItem?.ReceiverPartyId != null)
            .Select(o => o.FirstItem!.ReceiverPartyId!.Value)
            .Distinct()
            .ToList();

        var partyNames = receiverPartyIds.Count > 0
            ? await _financeDbContext.Parties
                .AsNoTracking()
                .Where(p => receiverPartyIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken)
            : new Dictionary<Guid, string>();

        var now = DateTime.UtcNow;
        var transactions = orders.Select(o =>
        {
            var receiverName = o.FirstItem?.ReceiverPartyId != null
                && partyNames.TryGetValue(o.FirstItem.ReceiverPartyId.Value, out var name)
                    ? name
                    : null;

            var title = BuildTitle(o.OrderType, receiverName);
            var subtitle = FormatTimestamp(o.CreatedAt);
            var amountLabel = FormatAmount(o.AmountIn, o.CurrencyIn);
            var type = MapOrderTypeToActivityType(o.OrderType);
            var dateGroupLabel = BuildDateGroupLabel(o.CreatedAt, now);

            return new PayActivityTransactionDto(
                Id: o.Id.ToString(),
                Title: title,
                Subtitle: subtitle,
                AmountLabel: amountLabel,
                Status: MapStatus(o.Status),
                Type: type,
                DateGroupLabel: dateGroupLabel);
        }).ToList();

        return new PayActivitySummaryResponse(transactions);
    }

    public async Task<PayActivityTransactionDetailResponse?> GetTransactionDetailAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var partyId = await ResolvePartyIdAsync(userId, tenantId, cancellationToken);
        if (partyId == null)
        {
            return null;
        }

        // Load the order with its items, ensuring it belongs to the current user
        var order = await _financeDbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.Id == transactionId
                        && o.TenantId == tenantId
                        && o.PayerPartyId == partyId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
        {
            return null;
        }

        // Load the associated payment intent for fee/total info
        var paymentIntent = await _financeDbContext.PaymentIntents
            .AsNoTracking()
            .Where(pi => pi.OrderId == order.Id && pi.TenantId == tenantId)
            .OrderByDescending(pi => pi.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Resolve receiver party for the first item
        var firstItem = order.Items.OrderBy(i => i.ItemIndex).FirstOrDefault();
        PayActivityRecipientDto recipient;

        if (firstItem?.ReceiverPartyId != null)
        {
            var receiverParty = await _financeDbContext.Parties
                .AsNoTracking()
                .Where(p => p.Id == firstItem.ReceiverPartyId.Value)
                .FirstOrDefaultAsync(cancellationToken);

            var displayName = receiverParty?.DisplayName ?? "Unknown";
            recipient = new PayActivityRecipientDto(
                Name: displayName,
                Initials: BuildInitials(displayName),
                BankName: string.Empty,
                MaskedAccountNumber: string.Empty,
                Country: order.DestinationCountry ?? string.Empty);
        }
        else
        {
            recipient = new PayActivityRecipientDto(
                Name: "Unknown",
                Initials: "??",
                BankName: string.Empty,
                MaskedAccountNumber: string.Empty,
                Country: string.Empty);
        }

        var fees = firstItem?.FeesTotal ?? 0m;
        var total = order.AmountIn + fees;

        return new PayActivityTransactionDetailResponse(
            Id: order.Id.ToString(),
            Status: MapStatus(order.Status),
            StatusDescription: BuildStatusDescription(order.Status, order.OrderType),
            AmountLabel: FormatAmount(order.AmountIn, order.CurrencyIn),
            FeeLabel: FormatAmount(fees, order.CurrencyIn),
            TotalLabel: FormatAmount(total, order.CurrencyIn),
            Recipient: recipient,
            OrderId: order.Id.ToString(),
            PaymentIntentId: paymentIntent?.Id.ToString() ?? string.Empty,
            ProviderReference: string.Empty,
            Reference: string.Empty);
    }

    // ═══════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
            throw new InvalidOperationException("Authenticated user is required.");
        return userId;
    }

    private async Task<Guid?> ResolvePartyIdAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        return await _financeDbContext.UserParties
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.UserId == userId)
            .OrderByDescending(link => link.Id)
            .Select(link => (Guid?)link.PartyId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string BuildTitle(string orderType, string? receiverName)
    {
        var prefix = orderType.Equals("Transfer", StringComparison.OrdinalIgnoreCase)
            ? "Transfer to"
            : orderType.Equals("BillPayment", StringComparison.OrdinalIgnoreCase)
                ? "Payment to"
                : "Payment to";

        return receiverName != null ? $"{prefix} {receiverName}" : prefix.TrimEnd(" to".ToCharArray());
    }

    private static string FormatTimestamp(DateTime createdAt)
    {
        var now = DateTime.UtcNow;
        if (createdAt.Date == now.Date)
            return $"Today, {createdAt:hh:mm tt}";
        if (createdAt.Date == now.Date.AddDays(-1))
            return $"Yesterday, {createdAt:hh:mm tt}";
        return createdAt.ToString("MMM d, yyyy, hh:mm tt", CultureInfo.InvariantCulture);
    }

    private static string FormatAmount(decimal amount, string currency)
    {
        return $"{currency} {amount:N2}";
    }

    private static string MapOrderTypeToActivityType(string orderType)
    {
        return orderType.Equals("BillPayment", StringComparison.OrdinalIgnoreCase)
            ? "bill"
            : "transfer";
    }

    private static string MapStatus(string status)
    {
        return status switch
        {
            "Completed" or "Settled" => "Completed",
            "Failed" or "Cancelled" => "Failed",
            _ => "Processing",
        };
    }

    private static string BuildDateGroupLabel(DateTime createdAt, DateTime now)
    {
        if (createdAt.Date == now.Date)
            return "TODAY";
        if (createdAt.Date == now.Date.AddDays(-1))
            return "YESTERDAY";
        return createdAt.ToString("MMM d", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    private static string BuildStatusDescription(string status, string orderType)
    {
        var kind = orderType.Equals("Transfer", StringComparison.OrdinalIgnoreCase)
            ? "transfer"
            : "payment";

        return status switch
        {
            "Completed" or "Settled" => $"This {kind} was successful.",
            "Failed" => $"This {kind} could not be completed.",
            "Cancelled" => $"This {kind} was cancelled.",
            _ => $"This {kind} is being processed.",
        };
    }

    private static string BuildInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        if (parts.Length == 1 && parts[0].Length >= 2)
            return parts[0][..2].ToUpperInvariant();
        return name.Length >= 2
            ? name[..2].ToUpperInvariant()
            : name.ToUpperInvariant();
    }
}
