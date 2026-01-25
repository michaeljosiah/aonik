namespace Aonik.Domain.Orders;

public static class OrderStatuses
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
