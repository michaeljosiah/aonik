namespace Aonik.Platform.Contracts.Api.PersonalFinance;

public record PayaboSetupProfileRequest(
    IReadOnlyList<string> SelectedUseCases,
    IReadOnlyList<string> AccountSourceTypes,
    string? ConnectChoice,
    IReadOnlyList<string> Responsibilities,
    string? SupportType,
    IReadOnlyList<string> FinancialGoals,
    bool Completed);

public record PayaboSetupProfileResponse(
    IReadOnlyList<string> SelectedUseCases,
    IReadOnlyList<string> AccountSourceTypes,
    string? ConnectChoice,
    IReadOnlyList<string> Responsibilities,
    string? SupportType,
    IReadOnlyList<string> FinancialGoals,
    bool Completed);

public record ClearPayaboSetupProfileResponse(string Status);
