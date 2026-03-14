using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Settings;

internal class PayaboSetupProfileService : IPayaboSetupProfileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PayaboSetupProfileService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PayaboSetupProfileSnapshot?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetRequiredContext();

        var payload = await _dbContext.Settings
            .AsNoTracking()
            .Where(setting => setting.Key == PayaboSettingNames.SetupProfile
                              && setting.Scope == SettingScope.User
                              && setting.TenantId == tenantId
                              && setting.UserId == userId)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<PayaboSetupProfileDocument>(payload, SerializerOptions);
        if (document == null)
        {
            return null;
        }

        return Map(document);
    }

    public async Task<PayaboSetupProfileSnapshot> SaveCurrentAsync(
        PayaboSetupProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetRequiredContext();

        var normalized = Normalize(profile);
        var document = new PayaboSetupProfileDocument(
            normalized.SelectedUseCases,
            normalized.AccountSourceTypes,
            normalized.ConnectChoice,
            normalized.Responsibilities,
            normalized.SupportType,
            normalized.FinancialGoals,
            normalized.Completed);

        var payload = JsonSerializer.Serialize(document, SerializerOptions);

        var existing = await _dbContext.Settings
            .FirstOrDefaultAsync(
                setting => setting.Key == PayaboSettingNames.SetupProfile
                           && setting.Scope == SettingScope.User
                           && setting.TenantId == tenantId
                           && setting.UserId == userId,
                cancellationToken);

        if (existing == null)
        {
            existing = new Setting
            {
                Key = PayaboSettingNames.SetupProfile,
                Scope = SettingScope.User,
                TenantId = tenantId,
                UserId = userId,
                Value = payload
            };

            _dbContext.Settings.Add(existing);
        }
        else
        {
            existing.Value = payload;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return normalized;
    }

    public async Task ClearCurrentAsync(CancellationToken cancellationToken = default)
    {
        await SaveCurrentAsync(
            new PayaboSetupProfileSnapshot(
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                Array.Empty<string>(),
                null,
                Array.Empty<string>(),
                false),
            cancellationToken);
    }

    private (Guid TenantId, Guid UserId) GetRequiredContext()
    {
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId) || tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context missing.");
        }

        if (!_currentUserProvider.TryGetCurrentUserId(out var userId) || userId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication required.");
        }

        return (tenantId, userId);
    }

    private static PayaboSetupProfileSnapshot Map(PayaboSetupProfileDocument document)
    {
        return Normalize(new PayaboSetupProfileSnapshot(
            document.SelectedUseCases,
            document.AccountSourceTypes,
            document.ConnectChoice,
            document.Responsibilities,
            document.SupportType,
            document.FinancialGoals,
            document.Completed));
    }

    private static PayaboSetupProfileSnapshot Normalize(PayaboSetupProfileSnapshot profile)
    {
        return new PayaboSetupProfileSnapshot(
            NormalizeList(profile.SelectedUseCases),
            NormalizeList(profile.AccountSourceTypes),
            NormalizeNullable(profile.ConnectChoice),
            NormalizeList(profile.Responsibilities),
            NormalizeNullable(profile.SupportType),
            NormalizeList(profile.FinancialGoals),
            profile.Completed);
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        if (values == null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private sealed record PayaboSetupProfileDocument(
        IReadOnlyList<string> SelectedUseCases,
        IReadOnlyList<string> AccountSourceTypes,
        string? ConnectChoice,
        IReadOnlyList<string> Responsibilities,
        string? SupportType,
        IReadOnlyList<string> FinancialGoals,
        bool Completed);
}
