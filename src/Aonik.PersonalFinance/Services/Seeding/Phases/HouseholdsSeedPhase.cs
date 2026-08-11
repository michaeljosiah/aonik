using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Seeding;

namespace Aonik.PersonalFinance.Services.Seeding.Phases;

/// <summary>
/// Seeds household groups and membership records for personal-finance demos.
/// </summary>
internal sealed class HouseholdsSeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    private readonly PersonalFinanceDbContext _db;

    public HouseholdsSeedPhase(PersonalFinanceDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedContext context,
        Dictionary<string, object> results,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;

        var households = new List<(Guid HouseholdId, string Name, Guid MemberId, string Role, string PermissionsJson)>
        {
            (SeedIds.Households.FamilyHouseholdId, "Mensah Household", SeedIds.Households.FamilyHouseholdMemberId, "Owner", JsonSerializer.Serialize(new[] { "Bills.Manage", "Goals.Manage" })),
            (SeedIds.Households.ProfessionalsHouseholdId, "Cross-Border Professionals", SeedIds.Households.ProfessionalsHouseholdMemberId, "Member", JsonSerializer.Serialize(new[] { "Bills.View", "Budget.View" }))
        };

        var householdIds = new List<Guid>();
        var householdMemberIds = new List<Guid>();

        // Spec 086 §13. P3 left this to the backfill, which was right while readers still used the
        // user columns. From P4/P5 they read party first, so a demo household whose members carry no
        // party would render as empty until an operator remembered to run a job — the worst kind of
        // seed bug, because it looks like the feature is broken rather than the data.
        var memberPartyId = userId.HasValue
            ? await _db.PersonalProfiles
                .AsNoTracking()
                .Where(profile => profile.TenantId == tenantId
                    && profile.UserId == userId.Value
                    && profile.PartyId != Guid.Empty)
                .Select(profile => (Guid?)profile.PartyId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        foreach (var seed in households)
        {
            var household = await _db.Households
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == seed.Name, cancellationToken);

            if (household == null)
            {
                household = new Household
                {
                    Id = seed.HouseholdId,
                    TenantId = tenantId,
                    Kind = GroupKinds.Household,
                    Name = seed.Name,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _db.Households.Add(household);
            }
            else
            {
                household.Name = seed.Name;
                household.UpdatedAt = now;
                household.UpdatedBy = userId;
            }

            householdIds.Add(household.Id);

            if (!userId.HasValue)
            {
                continue;
            }

            var existingMember = await _db.HouseholdMembers
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.HouseholdId == household.Id && item.UserId == userId.Value, cancellationToken);

            if (existingMember == null)
            {
                existingMember = new HouseholdMember
                {
                    Id = seed.MemberId,
                    TenantId = tenantId,
                    HouseholdId = household.Id,
                    UserId = userId.Value,
                    // Null when the profile is not seeded yet, which is not an error: the columns are
                    // dual-written, the readers fall back to the user, and the backfill closes it.
                    PartyId = memberPartyId,
                    Role = seed.Role,
                    PermissionsJson = seed.PermissionsJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _db.HouseholdMembers.Add(existingMember);
            }
            else
            {
                existingMember.PartyId ??= memberPartyId;
                existingMember.TenantId = tenantId;
                existingMember.Role = seed.Role;
                existingMember.PermissionsJson = seed.PermissionsJson;
                existingMember.UpdatedAt = now;
                existingMember.UpdatedBy = userId;
            }

            householdMemberIds.Add(existingMember.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);

        results[DemoSeedResultKeys.HouseholdIds] = householdIds;
        results[DemoSeedResultKeys.HouseholdMemberIds] = householdMemberIds;

        return new[] { "Seeded household groups for personal finance demos" };
    }
}
