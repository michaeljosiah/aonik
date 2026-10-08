using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;

namespace Aonik.Commerce.Services.Checkout;

internal static class ActiveBoxCarts
{
    public const int CandidateLimit = 20;

    public static IQueryable<Entities.Cart.Cart> ForParty(CommerceDbContext context, Guid tenantId, Guid partyId)
        => context.Carts.Where(cart => cart.TenantId == tenantId && cart.BuyerPartyId == partyId
            && cart.BoxBundleProductId != null && cart.Status == CartStatuses.Open
            && cart.OrderId == null && !cart.IsDeleted);

    public static ActiveBoxSnapshotDto Snapshot(Entities.Cart.Cart cart)
        => new(cart.Id, Convert.ToBase64String(cart.RowVersion), cart.BoxSize,
            cart.Items.Count(item => !item.IsDeleted), CartActivity.LastActivity(cart));

    public static ActiveBoxConflictException Conflict(string code, Entities.Cart.Cart? guest,
        IReadOnlyList<Entities.Cart.Cart> candidates)
        => new(code, "Choose which box to keep using the current cart versions.",
            guest is null ? null : Snapshot(guest),
            candidates.Take(CandidateLimit).Select(Snapshot).ToList(), candidates.Count > CandidateLimit);

    public static bool MatchesVersion(Entities.Cart.Cart cart, string? version)
    {
        if (version is null || version.Length > 64 || version.Length > 0 && string.IsNullOrWhiteSpace(version)) return false;
        try { return cart.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(version)); }
        catch (FormatException) { return false; }
    }
}
