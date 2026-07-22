using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Database.Tests.Support;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Commerce;

/// <summary>
/// Spec 072 Z2/Z3 under a real rowversion: two adoptions racing for the same guest cart both pass
/// the ownership check, so the Cart row's concurrency token is the only thing that serializes
/// them. The InMemory provider enforces no rowversion, so its suite is structurally unable to
/// fail here. The loser must answer from committed reality — the same party's double-submit is
/// the promised idempotent success; a competing party gets the same 404 as any other
/// unauthorized access, never a concurrency error that doubles as an oracle.
/// </summary>
public class CartAdoptionConcurrencySqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public CartAdoptionConcurrencySqlServerTests(SqlLocalDbFixture db) => _db = db;

    private sealed class WallClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private CartService NewCarts(Aonik.Commerce.Persistence.CommerceDbContext context, Guid tenantId)
        => new(context, new TestTenantProvider(tenantId),
            new ProductPricingService(context, new TestTenantProvider(tenantId), new WallClock()));

    [SkippableFact]
    public async Task SamePartyDoubleSubmit_Should_StayIdempotent_AcrossTheRace()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
        var tenantId = Guid.NewGuid();
        var party = Guid.NewGuid();
        var (cartId, token) = await SeedGuestCartAsync(tenantId);

        await using var contextA = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        await using var contextB = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var access = CartAccessContext.ForGuest(token);
        var results = await Task.WhenAll(
            Capture(Task.Run(() => NewCarts(contextA, tenantId).AdoptAsync(cartId, party, access))),
            Capture(Task.Run(() => NewCarts(contextB, tenantId).AdoptAsync(cartId, party, access))));

        results.Should().OnlyContain(r => r.Succeeded, "the same party's double-submit is idempotent");

        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var cart = await verify.Carts.AsNoTracking().SingleAsync(c => c.Id == cartId);
        cart.BuyerPartyId.Should().Be(party);
        cart.AnonymousToken.Should().BeNull("Z3 — adoption retires the guest token");
    }

    [SkippableFact]
    public async Task CompetingParties_Should_LeaveOneOwner_AndFailTheLoserClosed()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
        var tenantId = Guid.NewGuid();
        var partyA = Guid.NewGuid();
        var partyB = Guid.NewGuid();
        var (cartId, token) = await SeedGuestCartAsync(tenantId);

        await using var contextA = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        await using var contextB = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var access = CartAccessContext.ForGuest(token);
        var results = await Task.WhenAll(
            Capture(Task.Run(() => NewCarts(contextA, tenantId).AdoptAsync(cartId, partyA, access))),
            Capture(Task.Run(() => NewCarts(contextB, tenantId).AdoptAsync(cartId, partyB, access))));

        // The loser's path depends on the interleave — a pre-commit read loses at SaveChanges
        // (the concurrency catch), a post-commit read loses at the ownership check — but the
        // answer must be identical either way: the fail-closed 404. What must NEVER happen:
        // two owners, or a raw concurrency error leaking as a 409/500 oracle.
        var failures = results.Where(r => !r.Succeeded).ToList();
        results.Count(r => r.Succeeded).Should().Be(1, "exactly one party can win the cart");
        failures.Should().OnlyContain(
            r => r.Error is NotFoundException,
            "the loser gets the same 404 as any other unauthorized access — no oracle");

        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var cart = await verify.Carts.AsNoTracking().SingleAsync(c => c.Id == cartId);
        new[] { partyA, partyB }.Should().Contain(cart.BuyerPartyId!.Value);
        cart.AnonymousToken.Should().BeNull();
    }

    private async Task<(Guid CartId, string Token)> SeedGuestCartAsync(Guid tenantId)
    {
        await using var context = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var created = await NewCarts(context, tenantId).CreateCartAsync(new CreateCartCommand("GBP"));
        return (created.Id, created.AnonymousToken!);
    }

    private static async Task<(bool Succeeded, Exception? Error)> Capture(Task task)
    {
        try
        {
            await task;
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }
}
