using System.Data.Common;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests.Identity;

public sealed class CustomerAddressSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private static readonly FixedClock Clock = new();

    [SkippableFact]
    public async Task Create_Should_ReturnTheCommittedAddress_WhenCommitAcknowledgementIsLost()
    {
        RequireSql();
        var seed = await SeedAsync(0);
        var interruptedCommit = new LostCommitAcknowledgement();
        await using var db = Context(seed, interruptedCommit);

        var result = await Service(db, seed).CreateAsync(Address("1 Committed Street", seed.Version));

        interruptedCommit.CommittedAddressId.Should().NotBeNull();
        var address = result.Addresses.Should().ContainSingle().Subject;
        address.Id.Should().Be(interruptedCommit.CommittedAddressId!.Value);
        address.IsDefault.Should().BeTrue();
        result.DefaultAddressId.Should().Be(address.Id);
        result.Version.Should().Be(interruptedCommit.CommittedVersion);
        result.Version.Should().NotBe(seed.Version);
        Convert.FromBase64String(result.Version).Should().HaveCount(8);
        await db.SaveChangesAsync();
        (await AssertBookAsync(seed)).Should().BeEquivalentTo(result);
        await using var verify = Canonical(seed);
        (await verify.Set<PartyAddress>().IncludeSoftDeleted().CountAsync(item => item.PartyId == seed.PartyId))
            .Should().Be(1, "a retry after a successful commit must not add even a deleted duplicate");
    }

    [SkippableTheory]
    [InlineData("first-creates", 0)]
    [InlineData("two-defaults", 3)]
    [InlineData("select-versus-delete", 2)]
    [InlineData("delete-default-versus-create", 2)]
    public async Task ConcurrentMutations_Should_ClaimOneParentVersion_AndKeepOneLiveOwnedDefault(string scenario, int addressCount)
    {
        RequireSql();
        var seed = await SeedAsync(addressCount);
        var gate = new TwoWritesGate();
        await using var firstDb = Context(seed, gate);
        await using var secondDb = Context(seed, gate);
        firstDb.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        var firstService = Service(firstDb, seed);
        var secondService = Service(secondDb, seed);
        var first = scenario switch
        {
            "first-creates" => firstService.CreateAsync(Address("1 First Street", seed.Version)),
            "two-defaults" => firstService.SetDefaultAsync(seed.AddressIds[1], seed.Version),
            "select-versus-delete" => firstService.SetDefaultAsync(seed.AddressIds[1], seed.Version),
            _ => firstService.DeleteAsync(seed.AddressIds[0], seed.Version)
        };
        var second = scenario switch
        {
            "first-creates" => secondService.CreateAsync(Address("2 Second Street", seed.Version)),
            "two-defaults" => secondService.SetDefaultAsync(seed.AddressIds[2], seed.Version),
            "select-versus-delete" => secondService.DeleteAsync(seed.AddressIds[1], seed.Version),
            _ => secondService.CreateAsync(Address("3 New Street", seed.Version))
        };
        try
        {
            await AwaitBothAsync(gate.BothArrived.Task, first, second);
            gate.Release.TrySetResult();
            var outcomes = await Task.WhenAll(ObserveAsync(first), ObserveAsync(second)).WaitAsync(TimeSpan.FromSeconds(60));
            var winner = outcomes.Should().ContainSingle(outcome => outcome.Book != null).Subject.Book!;
            outcomes.Should().ContainSingle(outcome => outcome.Error is DbUpdateConcurrencyException);
            winner.Version.Should().NotBe(seed.Version);
            Convert.FromBase64String(winner.Version).Should().HaveCount(8);

            // Saving an unrelated user field in both scopes must not flush the losing address graph.
            await SaveUnrelatedUserFieldAsync(firstDb, seed, "+447700900111");
            await SaveUnrelatedUserFieldAsync(secondDb, seed, "+447700900222");
            var persisted = await AssertBookAsync(seed);
            persisted.Should().BeEquivalentTo(winner);
            if (scenario == "first-creates") persisted.Addresses.Should().ContainSingle();
            if (scenario == "two-defaults") persisted.Addresses.Should().HaveCount(3);
            if (scenario == "select-versus-delete")
            {
                if (outcomes[0].Book is not null)
                    persisted.DefaultAddressId.Should().Be(seed.AddressIds[1]);
                else
                    persisted.Addresses.Should().NotContain(address => address.Id == seed.AddressIds[1]);
            }
        }
        finally { gate.Release.TrySetResult(); }
    }

    [SkippableFact]
    public async Task SameClockAndStaleEditOrDelete_Should_RequireFreshParentVersion_WithoutChangingChildren()
    {
        RequireSql();
        var seed = await SeedAsync(2);
        await using var db = Context(seed);
        var service = Service(db, seed);
        var first = await service.UpdateAsync(seed.AddressIds[1], Address("Changed once", seed.Version));
        var second = await service.UpdateAsync(seed.AddressIds[1], Address("Changed twice", first.Version));

        second.Version.Should().NotBe(first.Version, "every mutation must claim the parent even with the same audit timestamp");
        var staleEdit = () => service.UpdateAsync(seed.AddressIds[1], Address("Rejected edit", seed.Version));
        await staleEdit.Should().ThrowAsync<DbUpdateConcurrencyException>();
        var staleDelete = () => service.DeleteAsync(seed.AddressIds[0], first.Version);
        await staleDelete.Should().ThrowAsync<DbUpdateConcurrencyException>();
        var missingVersion = () => service.CreateAsync(Address("Rejected create", null));
        await missingVersion.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await SaveUnrelatedUserFieldAsync(db, seed, "+447700900333");
        var persisted = await AssertBookAsync(seed);
        persisted.Should().BeEquivalentTo(second);
        persisted.DefaultAddressId.Should().Be(seed.AddressIds[0]);
        persisted.Addresses.Single(address => address.Id == seed.AddressIds[1]).Line1.Should().Be("Changed twice");
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSave_WithPreloadedGraph_Should_NotPersistRejectedAddressOnLaterUnrelatedSave(bool create)
    {
        RequireSql();
        var seed = await SeedAsync(2);
        var failure = new FailOneAddressSave();
        await using var db = Context(seed, failure);
        await db.Parties.SingleAsync(party => party.TenantId == seed.TenantId && party.Id == seed.PartyId);
        await db.PartyAddresses.Where(address => address.PartyId == seed.PartyId).LoadAsync();
        var service = Service(db, seed);
        var before = await service.GetAsync();
        before.Should().NotBeNull();
        var write = () => create
            ? service.CreateAsync(Address("Rejected new address", seed.Version))
            : service.UpdateAsync(seed.AddressIds[1], Address("Rejected replacement", seed.Version));

        await write.Should().ThrowAsync<DbUpdateException>().WithMessage("Injected address save failure.");
        await SaveUnrelatedUserFieldAsync(db, seed, "+447700900444");

        var persisted = await AssertBookAsync(seed);
        persisted.Should().BeEquivalentTo(before);
        await using var verify = Canonical(seed);
        var rows = await verify.Set<PartyAddress>().IncludeSoftDeleted()
            .Where(address => address.PartyId == seed.PartyId).ToListAsync();
        rows.Should().HaveCount(2, "the failed create must not reappear, even as a deleted child");
        rows.Should().OnlyContain(address => !address.IsDeleted && !address.Line1.StartsWith("Rejected"));
        (await verify.Users.SingleAsync(user => user.TenantId == seed.TenantId && user.Id == seed.UserId))
            .Phone.Should().Be("+447700900444", "the unrelated save really committed");
    }

    [SkippableFact]
    public async Task ForeignChildAndCorruptCrossTenantLink_Should_NotExposeOrMutateAnotherCustomersAddresses()
    {
        RequireSql();
        var own = await SeedAsync(1);
        var other = await SeedAsync(1);
        await using var db = Context(own);
        var service = Service(db, own);
        var updateForeign = () => service.UpdateAsync(other.AddressIds[0], Address("Rejected foreign edit", own.Version));
        await updateForeign.Should().ThrowAsync<NotFoundException>();
        var selectForeign = () => service.SetDefaultAsync(other.AddressIds[0], own.Version);
        await selectForeign.Should().ThrowAsync<NotFoundException>();
        var deleteForeign = () => service.DeleteAsync(other.AddressIds[0], own.Version);
        await deleteForeign.Should().ThrowAsync<NotFoundException>();
        await db.SaveChangesAsync();

        await using (var corrupt = Context(own))
        {
            var link = await corrupt.UserParties.SingleAsync(item => item.TenantId == own.TenantId && item.UserId == own.UserId);
            link.PartyId = other.PartyId;
            await corrupt.SaveChangesAsync();
        }
        await using var malformedDb = Context(own);
        var malformed = Service(malformedDb, own);
        (await malformed.GetAsync()).Should().BeNull();
        var malformedWrite = () => malformed.CreateAsync(Address("Rejected corrupt link", own.Version));
        await malformedWrite.Should().ThrowAsync<NotFoundException>();
        await malformedDb.SaveChangesAsync();

        var otherBook = await AssertBookAsync(other);
        otherBook.Version.Should().Be(other.Version);
        otherBook.Addresses.Should().ContainSingle().Which.Line1.Should().Be("1 Original Street");
        await using var verify = Canonical(own);
        var ownParty = await verify.Set<Party>().SingleAsync(party => party.TenantId == own.TenantId && party.Id == own.PartyId);
        Convert.ToBase64String(ownParty.RowVersion).Should().Be(own.Version);
        (await verify.Set<PartyAddress>().CountAsync(address => address.PartyId == own.PartyId)).Should().Be(1);
    }

    private async Task<CustomerAddressBookDto> AssertBookAsync(Seed seed)
    {
        await using var canonical = Canonical(seed);
        var parent = await canonical.Set<Party>().AsNoTracking()
            .SingleAsync(party => party.TenantId == seed.TenantId && party.Id == seed.PartyId && !party.IsDeleted);
        var children = await canonical.Set<PartyAddress>().AsNoTracking()
            .Where(address => address.PartyId == seed.PartyId && !address.IsDeleted).ToListAsync();
        parent.RowVersion.Should().HaveCount(8);
        children.Should().OnlyContain(address => address.RowVersion.Length == 8);
        if (children.Count == 0) parent.DefaultShippingAddressId.Should().BeNull();
        else children.Should().ContainSingle(address => address.Id == parent.DefaultShippingAddressId,
            "a committed default must be a live child of this exact tenant-owned party");

        await using var db = Context(seed);
        var book = await Service(db, seed).GetAsync();
        book.Should().NotBeNull();
        book!.Version.Should().Be(Convert.ToBase64String(parent.RowVersion));
        book.DefaultAddressId.Should().Be(parent.DefaultShippingAddressId);
        book.Addresses.Select(address => address.Id).Should().BeEquivalentTo(children.Select(address => address.Id));
        book.Addresses.Count(address => address.IsDefault).Should().Be(children.Count == 0 ? 0 : 1);
        return book;
    }

    private async Task<Seed> SeedAsync(int addressCount)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var seed = new Seed(tenantId, userId, partyId, [], string.Empty);
        await using var db = Context(seed);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = $"Address SQL {tenantId:N}", Status = "Active" });
        db.Users.Add(new User
        {
            Id = userId, TenantId = tenantId, ExternalIssuer = "https://identity.example.test/",
            ExternalSubject = $"auth0|{userId:N}", Email = $"{userId:N}@example.test", Status = "Active"
        });
        var party = new Party
        {
            Id = partyId, TenantId = tenantId, PartyType = "Person", DisplayName = "Address Customer", Status = "Active"
        };
        db.Parties.Add(party);
        db.UserParties.Add(new UserParty { TenantId = tenantId, UserId = userId, PartyId = partyId, LinkType = "Primary" });
        var addressIds = new List<Guid>();
        for (var index = 0; index < addressCount; index++)
        {
            var address = new PartyAddress
            {
                PartyId = partyId, Type = "Shipping", Line1 = $"{index + 1} Original Street",
                City = "London", Postcode = "SW1A 1AA", Country = "GB"
            };
            db.PartyAddresses.Add(address);
            addressIds.Add(address.Id);
        }
        party.DefaultShippingAddressId = addressIds.Count == 0 ? null : addressIds[0];
        await db.SaveChangesAsync();
        return seed with { AddressIds = addressIds, Version = Convert.ToBase64String(party.RowVersion) };
    }

    private PlatformDbContext Context(Seed seed, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>(database.CreateOptions<PlatformDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(seed.TenantId), new TestCurrentUserProvider(seed.UserId), Clock);
    }

    private AonikDbContext Canonical(Seed seed) => new(database.CreateOptions<AonikDbContext>(),
        new TestTenantProvider(seed.TenantId), new TestCurrentUserProvider(seed.UserId), Clock);

    private static CustomerAddressService Service(PlatformDbContext db, Seed seed)
    {
        var tenant = new TestTenantProvider(seed.TenantId);
        var user = new TestCurrentUserProvider(seed.UserId);
        var permissions = new Mock<IPermissionService>(MockBehavior.Strict);
        permissions.Setup(service => service.HasPermissionAsync(seed.UserId,
                It.Is<string>(permission => permission == "UserInfo.Read" || permission == "UserInfo.Update"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return new(db, tenant, user, new CurrentPartyResolver(db, user, tenant), permissions.Object, Clock);
    }

    private static CustomerAddressWrite Address(string line1, string? version) =>
        new("Shipping", line1, null, null, "London", null, "SW1A 1AA", "GB", version);

    private static async Task SaveUnrelatedUserFieldAsync(PlatformDbContext db, Seed seed, string phone)
    {
        var user = await db.Users.SingleAsync(item => item.TenantId == seed.TenantId && item.Id == seed.UserId);
        // Another test scope may already have changed this same user row.
        await db.Entry(user).ReloadAsync();
        user.Phone = phone;
        await db.SaveChangesAsync();
    }

    private void RequireSql() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");
    private static async Task<Outcome> ObserveAsync(Task<CustomerAddressBookDto> operation)
    {
        try { return new(await operation, null); }
        catch (Exception exception) { return new(null, exception); }
    }
    private static async Task AwaitBothAsync(Task reached, Task first, Task second)
    {
        var completed = await Task.WhenAny(reached, first, second).WaitAsync(TimeSpan.FromSeconds(30));
        if (completed != reached)
        {
            await completed;
            throw new InvalidOperationException("Address operation completed before the SQL race barrier.");
        }
    }
    private sealed record Seed(Guid TenantId, Guid UserId, Guid PartyId, IReadOnlyList<Guid> AddressIds, string Version);
    private sealed record Outcome(CustomerAddressBookDto? Book, Exception? Error);
    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class TwoWritesGate : SaveChangesInterceptor
    {
        private int _arrivals;
        public TaskCompletionSource BothArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Release.Task.IsCompleted && eventData.Context!.ChangeTracker.Entries<Party>()
                    .Any(entry => entry.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) BothArrived.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
    private sealed class FailOneAddressSave : SaveChangesInterceptor
    {
        private int _failed;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _failed, 1, 0) == 0)
            {
                // The parent and child SQL commands have run, but the service has not committed.
                eventData.Context!.Database.CurrentTransaction.Should().NotBeNull();
                throw new DbUpdateException("Injected address save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        private int _commits;
        public Guid? CommittedAddressId { get; private set; }
        public string? CommittedVersion { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction,
            TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _commits) == 1)
            {
                CommittedAddressId = eventData.Context!.ChangeTracker.Entries<PartyAddress>().Single().Entity.Id;
                CommittedVersion = Convert.ToBase64String(eventData.Context.ChangeTracker.Entries<Party>().Single().Entity.RowVersion);
                throw new TimeoutException("Test: SQL committed, but acknowledgement was lost.");
            }
            return Task.CompletedTask;
        }
    }
}
