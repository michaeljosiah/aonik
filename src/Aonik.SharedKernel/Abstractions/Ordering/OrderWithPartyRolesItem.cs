namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>
/// Cross-module projection that pairs an <see cref="OrderHistoryItem"/> with the
/// party-role assignments captured on the order. Used by Dashboard-style consumers
/// that need both order header data and the participating parties (payer / payee /
/// receiver / sender) in a single call.
/// </summary>
public sealed record OrderWithPartyRolesItem(
    OrderHistoryItem Order,
    IReadOnlyList<OrderPartyRoleItem> PartyRoles);

/// <summary>
/// Cross-module projection of an OrderPartyRole join row. The role string is
/// drawn from <see cref="OrderPartyRoleCodes"/>.
/// </summary>
public sealed record OrderPartyRoleItem(
    Guid PartyId,
    string Role);
