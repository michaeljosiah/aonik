namespace Aonik.SharedKernel.Abstractions.UserBrief;

/// <summary>
/// Cross-module contract for retrieving baseline identity and onboarding context
/// for the AI user brief.
/// </summary>
public interface IUserBriefContextDataProvider
{
    Task<UserBriefContextData> GetUserContextDataAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the primary user ID linked to a party within a tenant.
    /// Returns null if no user is linked.
    /// </summary>
    Task<Guid?> GetUserIdForPartyAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default);
}

public record UserBriefContextData(
    string? FullName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    DateTime? UserCreatedAt,
    UserBriefSetupProfileData? SetupProfile);

public record UserBriefSetupProfileData(
    IReadOnlyList<string> SelectedUseCases,
    IReadOnlyList<string> AccountSourceTypes,
    string? ConnectChoice,
    IReadOnlyList<string> Responsibilities,
    string? SupportType,
    IReadOnlyList<string> FinancialGoals,
    bool Completed);
