using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Finance;

/// <summary>
/// Spec 088 §8 — the three idempotency constraints, and the three shapes that must NOT collide.
///
/// This lane exists because the InMemory provider ignores index definitions entirely: every test
/// here would pass against InMemory whether the indexes existed, were unfiltered, or were the
/// wrong columns. Only the engine can tell the difference — and the filters are the whole point,
/// since an unfiltered unique index over a nullable column permits exactly one NULL per tenant and
/// would reject the second standalone invoice, or the second keyless payment intent, that the
/// platform raises routinely.
/// </summary>
public class InvoiceIntentIdempotencyIndexSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public InvoiceIntentIdempotencyIndexSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private FinanceDbContext CreateContext(Guid tenantId)
        => new(_db.CreateOptions<FinanceDbContext>(), new TestTenantProvider(tenantId), new TestCurrentUserProvider());

    private static Invoice NewInvoice(Guid tenantId, Guid? orderId = null, string? idempotencyKey = null) => new()
    {
        TenantId = tenantId,
        OrderId = orderId,
        IdempotencyKey = idempotencyKey,
        CustomerAccountId = Guid.NewGuid(),
        IssueDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(7),
        Currency = "GBP",
        Status = "Draft",
        ProvenanceJson = "{}"
    };

    private static PaymentIntent NewIntent(Guid tenantId, Guid orderId, string? idempotencyKey = null) => new()
    {
        TenantId = tenantId,
        OrderId = orderId,
        IdempotencyKey = idempotencyKey,
        Amount = 19.99m,
        Currency = "GBP",
        Status = "Pending",
        PurposeType = "Test",
        PurposeId = Guid.NewGuid()
    };

    private static async Task ShouldViolateUniqueIndexAsync(Func<Task> act, string indexName)
    {
        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        var sql = thrown.Which.InnerException.Should().BeOfType<SqlException>().Subject;
        sql.Number.Should().BeOneOf([2601, 2627]);
        sql.Message.Should().Contain(indexName);
    }

    // ---- the constraints -----------------------------------------------------------------

    [SkippableFact]
    public async Task SecondInvoice_ForTheSameOrder_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.Invoices.Add(NewInvoice(tenantId, orderId));
        await ctx.SaveChangesAsync();

        ctx.Invoices.Add(NewInvoice(tenantId, orderId));

        // A renewal that retries after its response was lost must not bill the customer twice.
        await ShouldViolateUniqueIndexAsync(() => ctx.SaveChangesAsync(), "IX_AnkInvoices_TenantId_OrderId");
    }

    [SkippableFact]
    public async Task SecondInvoice_WithTheSameIdempotencyKey_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.Invoices.Add(NewInvoice(tenantId, idempotencyKey: "sub:1:period:7"));
        await ctx.SaveChangesAsync();

        ctx.Invoices.Add(NewInvoice(tenantId, idempotencyKey: "sub:1:period:7"));

        await ShouldViolateUniqueIndexAsync(() => ctx.SaveChangesAsync(), "IX_AnkInvoices_TenantId_IdempotencyKey");
    }

    [SkippableFact]
    public async Task SecondIntent_WithTheSameIdempotencyKey_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.PaymentIntents.Add(NewIntent(tenantId, orderId, "sub:1:period:7:attempt:1"));
        await ctx.SaveChangesAsync();

        ctx.PaymentIntents.Add(NewIntent(tenantId, orderId, "sub:1:period:7:attempt:1"));

        await ShouldViolateUniqueIndexAsync(() => ctx.SaveChangesAsync(), "IX_AnkPaymentIntents_TenantId_IdempotencyKey");
    }

    // ---- the shapes that must NOT collide ------------------------------------------------

    [SkippableFact]
    public async Task TwoStandaloneInvoices_Should_BothSucceed()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.Invoices.Add(NewInvoice(tenantId, orderId: null));
        ctx.Invoices.Add(NewInvoice(tenantId, orderId: null));

        // THE regression this filter exists for. An unfiltered unique index permits one NULL per
        // tenant, so this is the second standalone invoice a tenant could never raise.
        var act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("standalone invoices are valid and the index is filtered on OrderId IS NOT NULL");

        (await ctx.Invoices.CountAsync(i => i.OrderId == null)).Should().Be(2);
    }

    [SkippableFact]
    public async Task ReplacementIntents_ForTheSameOrder_Should_BothSucceed()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.PaymentIntents.Add(NewIntent(tenantId, orderId));
        ctx.PaymentIntents.Add(NewIntent(tenantId, orderId));

        // Several intents per order are required, not merely tolerated: an abandoned checkout, a
        // switched payment method, a retried hard decline. Constraining by order would break the
        // public checkout for every product on the platform, not just subscriptions.
        var act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("idempotency comes from the key, never from forbidding retries");

        (await ctx.PaymentIntents.CountAsync(p => p.OrderId == orderId)).Should().Be(2);
    }

    [SkippableFact]
    public async Task RowsWithNoIdempotencyKey_Should_NotCollide()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.Invoices.Add(NewInvoice(tenantId, Guid.NewGuid()));
        ctx.Invoices.Add(NewInvoice(tenantId, Guid.NewGuid()));
        ctx.PaymentIntents.Add(NewIntent(tenantId, Guid.NewGuid()));
        ctx.PaymentIntents.Add(NewIntent(tenantId, Guid.NewGuid()));

        // Every row written before this phase has a null key, so an unfiltered index would have
        // made the migration itself unappliable against real data.
        var act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("the key indexes are filtered on IdempotencyKey IS NOT NULL");
    }

    [SkippableFact]
    public async Task TheSameKey_InAnotherTenant_Should_NotCollide()
    {
        RequireSqlServer();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var ctx = CreateContext(tenantA))
        {
            ctx.Invoices.Add(NewInvoice(tenantA, idempotencyKey: "renewal-1"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(tenantB))
        {
            ctx.Invoices.Add(NewInvoice(tenantB, idempotencyKey: "renewal-1"));

            // Keys are client-generated, so two tenants will collide eventually. Uniqueness is
            // tenant-scoped; otherwise the second tenant's insert fails against a row its query
            // filter cannot even see.
            var act = () => ctx.SaveChangesAsync();
            await act.Should().NotThrowAsync("idempotency keys are scoped per tenant");
        }
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
