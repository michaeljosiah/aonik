namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>
/// Order status code constants, mirrored from <c>Aonik.Finance.Entities.Orders.OrderStatuses</c>
/// so cross-module consumers (notably PersonalFinance) can interpret the
/// <see cref="OrderHistoryItem.Status"/> string without taking a dependency on
/// Finance entities.
///
/// The Finance-internal type remains the source of truth — these constants must
/// be kept in lockstep with it. Adding a new status requires updating both.
/// </summary>
public static class OrderStatusCodes
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Transmitted = "Transmitted";

    public const string Complete = "Complete";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
    public const string Expired = "Expired";

    public static bool IsTerminal(string? status)
        => status is Complete or Cancelled or Failed or Expired;
}
