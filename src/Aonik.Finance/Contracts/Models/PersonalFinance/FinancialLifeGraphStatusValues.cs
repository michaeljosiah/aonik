namespace Aonik.Finance.Contracts.Models.PersonalFinance;

public static class FinancialLifeGraphEntityStatuses
{
    public const string Active = "Active";
    public const string Proposed = "Proposed";
    public const string Rejected = "Rejected";

    public static readonly IReadOnlyList<string> All =
    [
        Active,
        Proposed,
        Rejected
    ];
}

public static class FinancialLifeGraphProposalStatuses
{
    public const string Proposed = "Proposed";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly IReadOnlyList<string> All =
    [
        Proposed,
        Approved,
        Rejected
    ];
}
