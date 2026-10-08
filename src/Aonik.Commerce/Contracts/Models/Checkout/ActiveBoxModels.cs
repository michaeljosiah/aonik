namespace Aonik.Commerce.Contracts.Models.Checkout;

public record ActiveBoxSnapshotDto(
    Guid CartId,
    string CartVersion,
    int? BoxSize,
    int LineCount,
    DateTime LastActivityAt);

public record AdoptCartChoice(
    string Decision,
    Guid ExpectedSavedCartId,
    string ExpectedSavedCartVersion,
    string ExpectedGuestCartVersion);

public static class CartAdoptionDecisions
{
    public const string KeepGuest = "KeepGuest";
    public const string UseSaved = "UseSaved";
}
