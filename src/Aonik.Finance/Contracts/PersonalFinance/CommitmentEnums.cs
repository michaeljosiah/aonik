namespace Aonik.Finance.Contracts.PersonalFinance;

/// <summary>
/// The nature of a recurring financial obligation.
/// </summary>
public enum CommitmentType
{
    /// <summary>A recurring non-debt obligation with a due date (e.g. council tax, electricity).</summary>
    Bill,

    /// <summary>A recurring merchant/service charge (e.g. Netflix, gym membership).</summary>
    Subscription,

    /// <summary>A recurring payment servicing a liability (e.g. mortgage, student loan).</summary>
    DebtRepayment,
}

/// <summary>
/// How confident the system is that this commitment is real and user-accepted.
/// Separate from <see cref="CommitmentOrigin"/> (how we learned about it)
/// and lifecycle <c>Status</c> (whether it's active/paused/archived).
/// </summary>
public enum VerificationStatus
{
    /// <summary>System inferred from transaction patterns; user hasn't reviewed yet.</summary>
    Detected,

    /// <summary>User has reviewed and accepted this commitment.</summary>
    Confirmed,

    /// <summary>User reviewed a detection and rejected it as not a real commitment.</summary>
    Rejected,
}

/// <summary>
/// How the system first learned about this commitment.
/// </summary>
public enum CommitmentOrigin
{
    /// <summary>User created the commitment manually.</summary>
    Manual,

    /// <summary>System detected the commitment from recurring transaction patterns.</summary>
    Detected,

    /// <summary>User promoted a specific transaction into a tracked commitment.</summary>
    PromotedFromTransaction,

    /// <summary>Imported from an external data source or bank feed.</summary>
    Imported,
}
