using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Services.Checkout;

public sealed class ActiveBoxConflictException(
    string code,
    string message,
    ActiveBoxSnapshotDto? guest,
    IReadOnlyList<ActiveBoxSnapshotDto> savedCandidates,
    bool hasMore = false) : Exception(message)
{
    public const string Existing = "commerce.active_box_exists";
    public const string ChoiceRequired = "commerce.box_choice_required";
    public const string StaleChoice = "commerce.box_choice_stale";
    public const string Multiple = "commerce.multiple_active_boxes";

    public string Code { get; } = code;
    public ActiveBoxSnapshotDto? Guest { get; } = guest;
    public IReadOnlyList<ActiveBoxSnapshotDto> SavedCandidates { get; } = savedCandidates;
    public bool HasMore { get; } = hasMore;
}
