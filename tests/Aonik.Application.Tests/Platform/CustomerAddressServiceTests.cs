using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Application.Tests.Platform;

public class CustomerAddressServiceTests
{
    [Fact]
    public async Task AddressBook_Should_NormalizeInternationalAddresses_AndMaintainOneDefaultThroughDeletion()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        await using var db = fixture.Context();
        var service = fixture.Service(db);
        var empty = (await service.GetAsync())!;
        empty.Addresses.Should().BeEmpty();
        empty.DefaultAddressId.Should().BeNull();
        var first = await service.CreateAsync(Address(empty.Version) with { Country = " ng ", Line1 = " 12 Lagos Road ", Line3 = " Upper floor " });
        var address = first.Addresses.Should().ContainSingle().Which;
        address.IsDefault.Should().BeTrue();
        address.Country.Should().Be("NG");
        address.Line1.Should().Be("12 Lagos Road");
        address.Line3.Should().Be("Upper floor");
        var second = await service.CreateAsync(Address(first.Version) with { Line1 = "20 Abuja Street" });
        var secondId = second.Addresses.Single(a => a.Id != address.Id).Id;
        second.DefaultAddressId.Should().Be(address.Id);
        var selected = await service.SetDefaultAsync(secondId, second.Version);
        selected.Addresses.Count(a => a.IsDefault).Should().Be(1);
        selected.DefaultAddressId.Should().Be(secondId);
        var edited = await service.UpdateAsync(address.Id, Address(selected.Version) with { Line1 = "Updated address" });
        edited.DefaultAddressId.Should().Be(secondId);
        var fallback = await service.DeleteAsync(secondId, edited.Version);
        fallback.DefaultAddressId.Should().Be(address.Id);
        var last = await service.DeleteAsync(address.Id, fallback.Version);
        last.Addresses.Should().BeEmpty();
        last.DefaultAddressId.Should().BeNull();
        var reread = (await service.GetAsync())!;
        reread.Addresses.Should().BeEmpty();
        reread.DefaultAddressId.Should().BeNull();
    }

    [Fact]
    public async Task LegacyBook_Should_ReadFullStoredFields_AndUseDeterministicFallbackWithoutWriting()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        await using var db = fixture.Context();
        var party = await db.Parties.SingleAsync();
        party.DefaultShippingAddressId = Guid.NewGuid();
        db.PartyAddresses.AddRange(new PartyAddress { Id = lowId, PartyId = fixture.PartyId, Type = "Legacy", Line1 = new string('x', 400), Line3 = "Third legacy line", City = "Lagos", Country = "Nigeria", Postcode = "" },
            new PartyAddress { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), PartyId = fixture.PartyId, Type = "Office", Line1 = "Later", City = "Lagos", Country = "NG" });
        await db.SaveChangesAsync();
        var stalePointer = party.DefaultShippingAddressId;
        db.ChangeTracker.Clear();

        var book = (await fixture.Service(db).GetAsync())!;

        book.DefaultAddressId.Should().Be(lowId);
        book.Addresses.First().Line1.Should().HaveLength(400);
        book.Addresses.First().Line3.Should().Be("Third legacy line");
        book.Addresses.First().Country.Should().Be("Nigeria");
        db.ChangeTracker.Entries().Should().BeEmpty();
        (await db.Parties.AsNoTracking().SingleAsync()).DefaultShippingAddressId.Should().Be(stalePointer);
        var saved = await fixture.Service(db).SetDefaultAsync(lowId, book.Version);
        saved.DefaultAddressId.Should().Be(lowId);
        (await db.Parties.AsNoTracking().SingleAsync()).DefaultShippingAddressId.Should().Be(lowId);
    }

    [Theory]
    [InlineData("blank")]
    [InlineData("control")]
    [InlineData("edge-control")]
    [InlineData("country")]
    [InlineData("long")]
    public async Task InvalidAddress_Should_FailWithoutCreatingOrChangingDefault(string invalid)
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        await using var db = fixture.Context();
        var command = invalid switch
        {
            "blank" => Address("") with { Line1 = " " },
            "control" => Address("") with { Line1 = "bad\naddress" },
            "edge-control" => Address("") with { Line1 = "\tAddress\n" },
            "country" => Address("") with { Country = "Nigeria" },
            _ => Address("") with { Line3 = new string('x', 257) }
        };
        var create = () => fixture.Service(db).CreateAsync(command);
        await create.Should().ThrowAsync<InvalidStateException>();
        (await db.PartyAddresses.CountAsync()).Should().Be(0);
        (await db.Parties.SingleAsync()).DefaultShippingAddressId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-base64")]
    [InlineData("AQ==")]
    public async Task MissingInvalidOrStaleVersion_Should_RejectBeforeMutation(string? version)
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        await using var db = fixture.Context();
        var create = () => fixture.Service(db).CreateAsync(Address(version));
        await create.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await db.PartyAddresses.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData("user-deleted")]
    [InlineData("user-inactive")]
    [InlineData("party-deleted")]
    [InlineData("link-deleted")]
    [InlineData("foreign-party")]
    public async Task MissingLiveOwnedProfile_Should_ReturnNoBook_AndNeverCreateOne(string state)
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        await using var db = fixture.Context();
        if (state == "user-deleted") (await db.Users.SingleAsync()).IsDeleted = true;
        if (state == "user-inactive") (await db.Users.SingleAsync()).Status = "Suspended";
        if (state == "party-deleted") (await db.Parties.SingleAsync()).IsDeleted = true;
        if (state == "link-deleted") (await db.UserParties.SingleAsync()).IsDeleted = true;
        if (state == "foreign-party")
        {
            var foreignTenant = Guid.NewGuid();
            await using var foreign = fixture.Context(foreignTenant);
            var otherParty = new Party { TenantId = foreignTenant, PartyType = "Person", DisplayName = "Other" };
            foreign.Parties.Add(otherParty);
            await foreign.SaveChangesAsync();
            (await db.UserParties.SingleAsync()).PartyId = otherParty.Id;
        }
        await db.SaveChangesAsync();

        var service = fixture.Service(db);
        (await service.GetAsync()).Should().BeNull();
        var create = () => service.CreateAsync(Address(""));
        await create.Should().ThrowAsync<NotFoundException>();
        (await db.PartyAddresses.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Permissions_Should_UseExistingSelfServiceReadAndUpdateKeys()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        await using var db = fixture.Context();
        fixture.Permissions.Setup(p => p.HasPermissionAsync(fixture.UserId, "UserInfo.Update", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        (await fixture.Service(db).GetAsync()).Should().NotBeNull();
        var write = () => fixture.Service(db).CreateAsync(Address(""));
        await write.Should().ThrowAsync<PermissionDeniedException>();
    }

    [Fact]
    public async Task FailedSave_Should_DetachOnlyTheAddressBook_AndNeverFlushItOnALaterUnrelatedSave()
    {
        var failure = new FailAddressSave();
        var fixture = new Fixture(failure);
        await fixture.SeedAsync();
        await using var db = fixture.Context();
        var user = await db.Users.SingleAsync();
        await db.Parties.Include(p => p.Addresses).SingleAsync();
        failure.Fail = true;
        var create = () => fixture.Service(db).CreateAsync(Address(""));
        await create.Should().ThrowAsync<DbUpdateException>();
        failure.Fail = false;
        user.Phone = "+234123456789";
        await db.SaveChangesAsync();
        (await db.PartyAddresses.AnyAsync()).Should().BeFalse();
        (await db.Parties.SingleAsync()).DefaultShippingAddressId.Should().BeNull();
        (await db.Users.SingleAsync()).Phone.Should().Be(user.Phone);
    }

    private static CustomerAddressWrite Address(string? version) => new("Shipping", "12 Main Road", null, null,
        "Lagos", null, "100001", "NG", version);

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid PartyId { get; } = Guid.NewGuid();
        public Mock<IPermissionService> Permissions { get; } = new();
        private readonly DbContextOptions<PlatformDbContext> _options;
        private readonly FixedClock _clock = new();

        public Fixture(params IInterceptor[] interceptors)
        {
            _options = new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase($"CustomerAddresses_{Guid.NewGuid()}")
                .AddInterceptors(interceptors).Options;
            Permissions.Setup(p => p.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        public PlatformDbContext Context(Guid? tenantId = null) => new(_options, new TestTenantProvider(tenantId ?? TenantId), new TestCurrentUserProvider(UserId), _clock);

        public CustomerAddressService Service(PlatformDbContext db)
        {
            var tenant = new TestTenantProvider(TenantId);
            var user = new TestCurrentUserProvider(UserId);
            return new(db, tenant, user, new CurrentPartyResolver(db, user, tenant), Permissions.Object, _clock);
        }

        public async Task SeedAsync()
        {
            await using var db = Context();
            db.Users.Add(new User { Id = UserId, TenantId = TenantId, Email = "customer@example.test" });
            db.Parties.Add(new Party { Id = PartyId, TenantId = TenantId, PartyType = "Person", DisplayName = "Customer", Status = "Active" });
            db.UserParties.Add(new UserParty { TenantId = TenantId, UserId = UserId, PartyId = PartyId });
            await db.SaveChangesAsync();
        }
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc); }

    private sealed class FailAddressSave : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Fail ? throw new DbUpdateException("Injected address save failure") : ValueTask.FromResult(result);
    }
}
