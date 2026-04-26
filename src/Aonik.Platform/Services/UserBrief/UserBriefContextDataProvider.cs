using System.Diagnostics;
using System.Text.Json;

using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.UserBrief;

internal sealed class UserBriefContextDataProvider : IUserBriefContextDataProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PlatformDbContext _dbContext;

    public UserBriefContextDataProvider(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserBriefContextData> GetUserContextDataAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Sequential queries — EF Core DbContext is not thread-safe
        var user = await TraceAsync(
            "aonik.user_brief.context.user",
            () => _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken));

        var partyLink = await TraceAsync(
            "aonik.user_brief.context.party_link",
            () => _dbContext.UserParties
                .AsNoTracking()
                .Where(link => link.UserId == userId && link.TenantId == tenantId)
                .OrderByDescending(link => link.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken));

        var setupPayload = await TraceAsync(
            "aonik.user_brief.context.setup_profile",
            () => _dbContext.Settings
                .AsNoTracking()
                .Where(setting => setting.Key == PayaboSettingNames.SetupProfile
                    && setting.TenantId == tenantId
                    && setting.UserId == userId)
                .Select(setting => setting.Value)
                .FirstOrDefaultAsync(cancellationToken));

        string? firstName = null;
        string? lastName = null;
        string? fullName = null;

        if (partyLink is not null)
        {
            var personProfile = await TraceAsync(
                "aonik.user_brief.context.person_profile",
                () => _dbContext.PersonProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(profile => profile.PartyId == partyLink.PartyId, cancellationToken));

            var party = await TraceAsync(
                "aonik.user_brief.context.party",
                () => _dbContext.Parties
                    .AsNoTracking()
                    .FirstOrDefaultAsync(party => party.Id == partyLink.PartyId, cancellationToken));

            firstName = personProfile?.FirstName?.Trim();
            lastName = personProfile?.LastName?.Trim();
            fullName = JoinName(firstName, lastName)
                ?? Normalize(party?.DisplayName)
                ?? DeriveNameFromEmail(user?.Email)
                ?? $"User {userId:N}";
        }
        else
        {
            fullName = DeriveNameFromEmail(user?.Email) ?? $"User {userId:N}";
            firstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        }

        return new UserBriefContextData(
            FullName: fullName,
            FirstName: firstName,
            LastName: lastName,
            Email: Normalize(user?.Email),
            PhoneNumber: Normalize(user?.Phone),
            UserCreatedAt: user?.CreatedAt,
            SetupProfile: ParseSetupProfile(setupPayload));
    }

    public async Task<Guid?> GetUserIdForPartyAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserParties
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.PartyId == partyId)
            .OrderByDescending(link => link.CreatedAt)
            .Select(link => (Guid?)link.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<T> TraceAsync<T>(string name, Func<Task<T>> operation)
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity(name, ActivityKind.Internal);
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            throw;
        }
    }

    private static UserBriefSetupProfileData? ParseSetupProfile(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<PayaboSetupProfileDocument>(payload, SerializerOptions);
        if (document is null)
        {
            return null;
        }

        return new UserBriefSetupProfileData(
            NormalizeList(document.SelectedUseCases),
            NormalizeList(document.AccountSourceTypes),
            Normalize(document.ConnectChoice),
            NormalizeList(document.Responsibilities),
            Normalize(document.SupportType),
            NormalizeList(document.FinancialGoals),
            document.Completed);
    }

    private static string? JoinName(string? firstName, string? lastName)
    {
        var parts = new[] { Normalize(firstName), Normalize(lastName) }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    private static string? DeriveNameFromEmail(string? email)
    {
        var localPart = Normalize(email)?.Split('@', 2, StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(localPart))
        {
            return null;
        }

        var parts = localPart
            .Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant())
            .ToArray();

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record PayaboSetupProfileDocument(
        IReadOnlyList<string> SelectedUseCases,
        IReadOnlyList<string> AccountSourceTypes,
        string? ConnectChoice,
        IReadOnlyList<string> Responsibilities,
        string? SupportType,
        IReadOnlyList<string> FinancialGoals,
        bool Completed);
}
