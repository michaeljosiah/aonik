namespace Aonik.Platform.Contracts.Models.Settings;

public record PayaboSetupProfileSnapshot(
    IReadOnlyList<string> SelectedUseCases,
    IReadOnlyList<string> AccountSourceTypes,
    string? ConnectChoice,
    IReadOnlyList<string> Responsibilities,
    string? SupportType,
    IReadOnlyList<string> FinancialGoals,
    bool Completed);
