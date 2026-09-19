using Aonik.Groups.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Application.Tests.PersonalFinance;

public sealed class FamilyProfileLimitTests
{
    [Fact]
    public async Task Limit_Should_CountOtherGuardiansChildren_AndReturnCapacityAfterRemoval()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new TestTenantProvider(tenantId);
        using var db = new PersonalFinanceDbContext(new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options, tenant);
        var group = Guid.NewGuid();
        var existingChild = Guid.NewGuid();
        var newChild = Guid.NewGuid();
        var member = new HouseholdMember {TenantId = tenantId, HouseholdId = group, PartyId = existingChild,
            Role = GroupRoles.Viewer, InvitationStatus = GroupMemberStatuses.Accepted};
        db.HouseholdMembers.Add(member);
        await db.SaveChangesAsync();
        var bands = new Mock<ISafetyBandReader>();
        bands.Setup(b => b.GetSafetyBandAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync("under-6");
        var entitlements = new Mock<IEntitlementReader>();
        entitlements.Setup(e => e.GetMeterAsync(It.IsAny<SubscriberRef>(), "child-profiles", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeterEntitlement("child-profiles", MeterKinds.Ceiling, "profiles", 1, 0, 0, 1, ResetPolicies.Never, null));
        using var services = new ServiceCollection().AddSingleton(bands.Object).AddSingleton(entitlements.Object).BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Groups:ProfileLimits:TenantIds:0"] = tenantId.ToString() }).Build();
        var guard = new FamilyProfileLimitContributor(db, tenant, config, services);
        var transition = new GroupTransition(GroupTransitionKinds.MemberAdded, group, newChild, null,
            Guid.NewGuid(), GroupKind: GroupKinds.Family);
        (await guard.VetoAsync(transition)).Should().Contain("limit");
        db.HouseholdMembers.Remove(member);
        await db.SaveChangesAsync();
        (await guard.VetoAsync(transition)).Should().BeNull();
        entitlements.Setup(e => e.GetMeterAsync(It.IsAny<SubscriberRef>(), "child-profiles", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeterEntitlement?)null);
        (await guard.VetoAsync(transition)).Should().Contain("Choose a family plan");
    }
}
