namespace Aonik.Agents.Entities;

/// <summary>
/// Lifecycle of an agent <c>Proposal</c>. Spec 032 §8.1 split the original
/// three-value enum so a money decision distinguishes "a human approved this"
/// (<see cref="Approved"/>) from "the system executed it" (<see cref="Applied"/> /
/// <see cref="Failed"/>). Persisted as a string (see <c>ProposalConfiguration</c>),
/// so the two new values are a code-only change — no migration.
/// </summary>
public enum ProposalStatus
{
    /// <summary>Created and awaiting a decision.</summary>
    Proposed = 1,

    /// <summary>A human/policy approved it; execution not yet confirmed.</summary>
    Approved = 2,

    /// <summary>Dismissed without execution. Terminal.</summary>
    Rejected = 3,

    /// <summary>Dispatcher ran the handler and it reported success. Terminal.</summary>
    Applied = 4,

    /// <summary>
    /// Handler threw or reported <c>Applied = false</c>. Terminal for High-risk
    /// (money) proposals: retry is a brand-new proposal, never a re-approval of
    /// this one, so a capture whose outcome is uncertain cannot double-move funds.
    /// </summary>
    Failed = 5
}
