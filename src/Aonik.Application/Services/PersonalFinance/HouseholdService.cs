using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.PersonalFinance;
using Aonik.Domain.PersonalFinance.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.PersonalFinance;

public class HouseholdService : IHouseholdService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly IReadOnlyList<string> EmptyPermissions = Array.Empty<string>();
    private const string OwnerRole = "Owner";

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public HouseholdService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
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

        var alreadyMember = await _dbContext.HouseholdMembers
            .AnyAsync(member => member.UserId == userId, cancellationToken);

        if (alreadyMember)
        {
            throw new InvalidOperationException("User already belongs to a household.");
        }

        var household = new Household
        {
            TenantId = tenantId,
            Name = request.Name.Trim()
        };

        _dbContext.Households.Add(household);

        var member = new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = userId,
            Role = OwnerRole,
            PermissionsJson = JsonSerializer.Serialize(EmptyPermissions, JsonOptions)
        };

        _dbContext.HouseholdMembers.Add(member);

        await AssignHouseholdToProfileAsync(userId, tenantId, household.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        var household = await _dbContext.Households
            .FirstOrDefaultAsync(h => h.Id == request.HouseholdId && h.TenantId == tenantId, cancellationToken);

        if (household == null)
        {
            throw new InvalidOperationException("Household not found.");
        }

        var inviterIsMember = await _dbContext.HouseholdMembers
            .AnyAsync(member => member.HouseholdId == household.Id && member.UserId == currentUserId, cancellationToken);

        if (!inviterIsMember)
        {
            throw new InvalidOperationException("Only household members can invite others.");
        }

        var userExists = await _dbContext.Users
            .AnyAsync(user => user.Id == request.UserId && user.TenantId == tenantId, cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException("User not found.");
        }

        var alreadyMember = await _dbContext.HouseholdMembers
            .AnyAsync(member => member.HouseholdId == household.Id && member.UserId == request.UserId, cancellationToken);

        if (alreadyMember)
        {
            throw new InvalidOperationException("User is already a member of this household.");
        }

        var anyMembership = await _dbContext.HouseholdMembers
            .AnyAsync(member => member.UserId == request.UserId, cancellationToken);

        if (anyMembership)
        {
            throw new InvalidOperationException("User already belongs to a household.");
        }

        var permissions = NormalizePermissions(request.Permissions);

        var member = new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = request.UserId,
            Role = request.Role.Trim(),
            PermissionsJson = JsonSerializer.Serialize(permissions, JsonOptions)
        };

        _dbContext.HouseholdMembers.Add(member);

        await AssignHouseholdToProfileAsync(request.UserId, tenantId, household.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
        var profile = await _dbContext.PersonalProfiles
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
