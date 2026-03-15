using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.PersonalFinance;

internal class HouseholdService : Contracts.Services.PersonalFinance.IHouseholdService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly IReadOnlyList<string> EmptyPermissions = Array.Empty<string>();
    private const string OwnerRole = "Owner";

    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public HouseholdService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<HouseholdResponse> CreateHouseholdAsync(
        CreateHouseholdRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Household name is required.", nameof(request.Name));
        }

        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        await EnsurePersonalProfileAsync(userId, tenantId, cancellationToken);

        var alreadyMember = await _financeDbContext.HouseholdMembers
            .AnyAsync(member => member.TenantId == tenantId && member.UserId == userId, cancellationToken);

        if (alreadyMember)
        {
            throw new InvalidOperationException("User already belongs to a household.");
        }

        var household = new Household
        {
            TenantId = tenantId,
            Name = request.Name.Trim()
        };

        _financeDbContext.Households.Add(household);

        var member = new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = household.Id,
            UserId = userId,
            Role = OwnerRole,
            PermissionsJson = JsonSerializer.Serialize(EmptyPermissions, JsonOptions)
        };

        _financeDbContext.HouseholdMembers.Add(member);

        await AssignHouseholdToProfileAsync(userId, tenantId, household.Id, cancellationToken);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        _cacheInvalidator.InvalidateCurrentUserGraph();

        var ownerResponse = new HouseholdMemberResponse(
            member.Id,
            household.Id,
            member.UserId,
            member.Role,
            EmptyPermissions,
            member.CreatedAt);

        return new HouseholdResponse(
            household.Id,
            household.Name,
            ownerResponse,
            household.CreatedAt);
    }

    public async Task<HouseholdMemberResponse> InviteMemberAsync(
        InviteHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.HouseholdId == Guid.Empty)
        {
            throw new ArgumentException("HouseholdId is required.", nameof(request.HouseholdId));
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(request.UserId));
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            throw new ArgumentException("Role is required.", nameof(request.Role));
        }

        if (!_currentUserProvider.TryGetCurrentUserId(out var currentUserId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var household = await _financeDbContext.Households
            .FirstOrDefaultAsync(h => h.Id == request.HouseholdId && h.TenantId == tenantId, cancellationToken);

        if (household == null)
        {
            throw new InvalidOperationException("Household not found.");
        }

        var inviterIsMember = await _financeDbContext.HouseholdMembers
            .AnyAsync(member => member.TenantId == tenantId && member.HouseholdId == household.Id && member.UserId == currentUserId, cancellationToken);

        if (!inviterIsMember)
        {
            throw new InvalidOperationException("Only household members can invite others.");
        }

        await EnsurePersonalProfileAsync(request.UserId, tenantId, cancellationToken);

        var userExists = await _financeDbContext.Users
            .AnyAsync(user => user.Id == request.UserId && user.TenantId == tenantId, cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException("User not found.");
        }

        var alreadyMember = await _financeDbContext.HouseholdMembers
            .AnyAsync(member => member.TenantId == tenantId && member.HouseholdId == household.Id && member.UserId == request.UserId, cancellationToken);

        if (alreadyMember)
        {
            throw new InvalidOperationException("User is already a member of this household.");
        }

        var anyMembership = await _financeDbContext.HouseholdMembers
            .AnyAsync(member => member.TenantId == tenantId && member.UserId == request.UserId, cancellationToken);

        if (anyMembership)
        {
            throw new InvalidOperationException("User already belongs to a household.");
        }

        var permissions = NormalizePermissions(request.Permissions);

        var member = new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = household.Id,
            UserId = request.UserId,
            Role = request.Role.Trim(),
            PermissionsJson = JsonSerializer.Serialize(permissions, JsonOptions)
        };

        _financeDbContext.HouseholdMembers.Add(member);

        await AssignHouseholdToProfileAsync(request.UserId, tenantId, household.Id, cancellationToken);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        _cacheInvalidator.InvalidateCurrentUserGraph();

        return new HouseholdMemberResponse(
            member.Id,
            household.Id,
            member.UserId,
            member.Role,
            permissions,
            member.CreatedAt);
    }

    private async Task AssignHouseholdToProfileAsync(
        Guid userId,
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var profile = await _financeDbContext.PersonalProfiles
            .FirstOrDefaultAsync(item => item.UserId == userId && item.TenantId == tenantId, cancellationToken);

        if (profile == null)
        {
            throw new InvalidOperationException("Personal profile is required to manage household membership.");
        }

        if (profile.HouseholdId.HasValue && profile.HouseholdId.Value != householdId)
        {
            throw new InvalidOperationException("User already belongs to a household.");
        }

        profile.HouseholdId = householdId;
    }

    private async Task EnsurePersonalProfileAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var profileExists = await _financeDbContext.PersonalProfiles
            .AnyAsync(item => item.UserId == userId && item.TenantId == tenantId, cancellationToken);

        if (!profileExists)
        {
            throw new InvalidOperationException("Personal profile is required to manage household membership.");
        }
    }

    private static IReadOnlyList<string> NormalizePermissions(IReadOnlyList<string>? permissions)
    {
        if (permissions == null || permissions.Count == 0)
        {
            return EmptyPermissions;
        }

        return permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
