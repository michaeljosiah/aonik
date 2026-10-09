using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

using Aonik.Platform.Contracts.Services.Storage;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Party;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Caching;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Payments;

public class CheckoutGuestPartyTests
{
    [Fact]
    public async Task Ensure_Should_CreateOneUnverifiedGuestAndReplayWithoutOverwritingCapturedDetails()
    {
        using var test = new Harness();
        var partyId = Guid.NewGuid();
        var checkoutId = Guid.NewGuid();

        var first = await test.Service.EnsureUnverifiedGuestPartyAsync(partyId, checkoutId, Details());
        var replay = await test.Service.EnsureUnverifiedGuestPartyAsync(partyId, checkoutId,
            Details() with { DisplayName = "Changed", Email = "other@example.test" });

        replay.Should().Be(first);
        var party = await test.Db.Parties.Include(p => p.Contacts).SingleAsync();
        party.Id.Should().Be(partyId);
        party.TenantId.Should().Be(test.TenantId);
        party.DisplayName.Should().Be("Guest Purchaser");
        party.Contacts.Should().Contain(c => c.Type == "Email" && c.Value == "guest@example.test");
        (await test.Db.PersonProfiles.SingleAsync()).IdvStatus.Should().Be("Unverified");
        var provenance = await test.Db.PartyRoleAssignments.SingleAsync();
        provenance.Role.Should().Be("Customer");
        provenance.ContextType.Should().Be("CommerceCheckout");
        provenance.ContextId.Should().Be(checkoutId);
        provenance.PartyId.Should().Be(partyId);
        test.Db.Users.Should().BeEmpty();
        test.Db.UserParties.Should().BeEmpty();
    }

    [Fact]
    public async Task Ensure_Should_NotClaimExistingPartyOrUserByEmail()
    {
        using var test = new Harness();
        var existing = await test.Service.CreatePartyAsync(Details());
        var guestId = Guid.NewGuid();

        var guest = await test.Service.EnsureUnverifiedGuestPartyAsync(guestId, Guid.NewGuid(), Details());

        guest.PartyId.Should().Be(guestId).And.NotBe(existing.PartyId);
        (await test.Db.Parties.CountAsync()).Should().Be(2);
        test.Db.UserParties.Should().BeEmpty();
        test.Db.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Ensure_Should_RejectExistingPartyWithoutExactCheckoutProvenance()
    {
        using var test = new Harness();
        var existing = await test.Service.CreatePartyAsync(Details());

        await test.Service.Invoking(s => s.EnsureUnverifiedGuestPartyAsync(existing.PartyId, Guid.NewGuid(), Details()))
            .Should().ThrowAsync<InvalidStateException>();

        test.Db.PartyRoleAssignments.Should().BeEmpty();
        (await test.Db.Parties.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Ensure_Should_RejectReplayForDifferentCheckout()
    {
        using var test = new Harness();
        var partyId = Guid.NewGuid();
        var checkoutId = Guid.NewGuid();
        await test.Service.EnsureUnverifiedGuestPartyAsync(partyId, checkoutId, Details());

        await test.Service.Invoking(s => s.EnsureUnverifiedGuestPartyAsync(partyId, Guid.NewGuid(), Details()))
            .Should().ThrowAsync<InvalidStateException>();

        (await test.Db.PartyRoleAssignments.SingleAsync()).ContextId.Should().Be(checkoutId);
        (await test.Db.Parties.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Ensure_Should_KeepSameEmailInDifferentTenantsIndependent()
    {
        var databaseName = $"CheckoutGuests_{Guid.NewGuid()}";
        using var first = new Harness(databaseName);
        using var second = new Harness(databaseName);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await first.Service.EnsureUnverifiedGuestPartyAsync(firstId, Guid.NewGuid(), Details());
        await second.Service.EnsureUnverifiedGuestPartyAsync(secondId, Guid.NewGuid(), Details());

        (await first.Db.Parties.SingleAsync()).Id.Should().Be(firstId);
        (await second.Db.Parties.SingleAsync()).Id.Should().Be(secondId);
        (await first.Db.PartyRoleAssignments.SingleAsync()).TenantId.Should().Be(first.TenantId);
        (await second.Db.PartyRoleAssignments.SingleAsync()).TenantId.Should().Be(second.TenantId);
        first.Db.UserParties.Should().BeEmpty();
        second.Db.UserParties.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Business", false, false)]
    [InlineData("Person", true, false)]
    [InlineData("Person", false, true)]
    public async Task Ensure_Should_RejectMissingStableIdsOrNonPerson(string partyType, bool emptyParty, bool emptyCheckout)
    {
        using var test = new Harness();

        await test.Service.Invoking(s => s.EnsureUnverifiedGuestPartyAsync(
                emptyParty ? Guid.Empty : Guid.NewGuid(), emptyCheckout ? Guid.Empty : Guid.NewGuid(), Details() with { PartyType = partyType }))
            .Should().ThrowAsync<ArgumentException>();

        test.Db.Parties.Should().BeEmpty();
        test.Db.PartyRoleAssignments.Should().BeEmpty();
    }

    private static CreatePartyRequest Details() => new("Guest Purchaser", "Person", "Guest", "Purchaser",
        "+447700900123", "guest@example.test", "GB");

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public PlatformDbContext Db { get; }
        public PartyService Service { get; }

        public Harness(string? databaseName = null)
        {
            var tenant = new TestTenantProvider(TenantId);
            var clock = Mock.Of<IClock>(c => c.UtcNow == new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));
            Db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase(databaseName ?? $"CheckoutGuests_{Guid.NewGuid()}").Options, tenant, null, clock);
            Service = new PartyService(Db, tenant, clock, Mock.Of<IAuditLogWriter>(),
                Mock.Of<ICacheInvalidationPublisher>(), Mock.Of<IProfilePhotoStore>());
        }

        public void Dispose() => Db.Dispose();
    }
}
