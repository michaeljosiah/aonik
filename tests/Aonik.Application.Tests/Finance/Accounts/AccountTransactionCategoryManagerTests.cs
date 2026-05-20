using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Entities.Accounts;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Accounts;
using Aonik.Finance.Services.Accounts.Linking;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance.Accounts;

public class AccountTransactionCategoryManagerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid result) { result = tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider(Guid userId) : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => userId;
        public bool TryGetCurrentUserId(out Guid result) { result = userId; return true; }
    }

    private static FinanceDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"CategoryMgrTests_{Guid.NewGuid()}")
            .Options;
        return new FinanceDbContext(options, new TestTenantProvider(TenantId));
    }

    private static AccountTransactionCategoryManager NewManager(FinanceDbContext context)
    {
        var mapper = new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper();
        return new AccountTransactionCategoryManager(
            context,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(UserId),
            new AccountTransactionCategorizer(mapper),
            mapper);
    }

    private static AccountTransaction Seed(
        FinanceDbContext context,
        string? category = null,
        string? counterparty = "Some Merchant",
        Guid? connectionId = null,
        DateTime? lockedAt = null,
        Guid? tenantId = null)
    {
        var tx = new AccountTransaction
        {
            TenantId = tenantId ?? TenantId,
            AccountId = Guid.NewGuid(),
            AccountConnectionId = connectionId,
            ProviderTransactionReference = $"tx-{Guid.NewGuid():N}",
            OccurredAt = DateTime.UtcNow,
            Amount = -25m,
            Currency = "USD",
            Counterparty = counterparty,
            Category = category,
            CategoryLockedAt = lockedAt,
            ReconciliationStatus = "Unmatched",
        };
        context.AccountTransactions.Add(tx);
        context.SaveChanges();
        return tx;
    }

    [Fact]
    public async Task SetCategoryAsync_Should_Lock_And_UpdateFields()
    {
        await using var context = NewContext();
        var tx = Seed(context);
        var sut = NewManager(context);

        var result = await sut.SetCategoryAsync(
            tx.Id,
            new SetAccountTransactionCategoryRequest("bills", "phone", false),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Category.Should().Be("bills");
        result.SubCategory.Should().Be("phone");
        result.CategoryMethod.Should().Be("manual");
        result.CategoryConfidence.Should().Be(1.00m);
        result.CategoryLockedAt.Should().NotBeNull();
        result.MerchantRuleCreated.Should().BeFalse();
    }

    [Fact]
    public async Task SetCategoryAsync_Should_UpsertMerchantRule_When_RememberForMerchant_True()
    {
        await using var context = NewContext();
        var tx = Seed(context, counterparty: "Acme Coffee Ltd");
        var sut = NewManager(context);

        var result = await sut.SetCategoryAsync(
            tx.Id,
            new SetAccountTransactionCategoryRequest("eating_out", "cafe", true),
            CancellationToken.None);

        result!.MerchantRuleCreated.Should().BeTrue();

        var rule = await context.AccountTransactionMerchantCategories
            .SingleAsync(r => r.TenantId == TenantId);
        rule.MerchantKey.Should().Be("acme coffee");
        rule.Category.Should().Be("eating_out");
        rule.SubCategory.Should().Be("cafe");
    }

    [Fact]
    public async Task SetCategoryAsync_Should_Upsert_NotDuplicate_OnSecondCall()
    {
        await using var context = NewContext();
        var tx1 = Seed(context, counterparty: "Spotify AB");
        var sut = NewManager(context);

        await sut.SetCategoryAsync(tx1.Id,
            new SetAccountTransactionCategoryRequest("entertainment", null, true),
            CancellationToken.None);

        var tx2 = Seed(context, counterparty: "Spotify AB");
        var result = await sut.SetCategoryAsync(tx2.Id,
            new SetAccountTransactionCategoryRequest("subscriptions", "music", true),
            CancellationToken.None);

        result!.MerchantRuleCreated.Should().BeFalse();

        var rules = await context.AccountTransactionMerchantCategories
            .Where(r => r.TenantId == TenantId)
            .ToListAsync();
        rules.Should().HaveCount(1);
        rules[0].Category.Should().Be("subscriptions");
        rules[0].SubCategory.Should().Be("music");
    }

    [Fact]
    public async Task SetCategoryAsync_Should_Throw_When_CategoryInvalid()
    {
        await using var context = NewContext();
        var tx = Seed(context);
        var sut = NewManager(context);

        var act = () => sut.SetCategoryAsync(
            tx.Id,
            new SetAccountTransactionCategoryRequest("not_a_real_code", null, false),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetCategoryAsync_Should_Throw_When_SubCategory_Invalid_For_Category()
    {
        await using var context = NewContext();
        var tx = Seed(context);
        var sut = NewManager(context);

        var act = () => sut.SetCategoryAsync(
            tx.Id,
            new SetAccountTransactionCategoryRequest("bills", "supermarket", false),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetCategoryAsync_Should_Return_Null_When_CrossTenant()
    {
        await using var context = NewContext();
        var tx = Seed(context, tenantId: OtherTenantId);
        var sut = NewManager(context);

        var result = await sut.SetCategoryAsync(
            tx.Id,
            new SetAccountTransactionCategoryRequest("bills", null, false),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UnlockCategoryAsync_Should_ClearLock()
    {
        await using var context = NewContext();
        var tx = Seed(context, lockedAt: DateTime.UtcNow, category: "bills");
        var sut = NewManager(context);

        var ok = await sut.UnlockCategoryAsync(tx.Id, CancellationToken.None);

        ok.Should().BeTrue();
        var refreshed = await context.AccountTransactions.SingleAsync(t => t.Id == tx.Id);
        refreshed.CategoryLockedAt.Should().BeNull();
        refreshed.Category.Should().Be("bills");
    }

    [Fact]
    public async Task DeleteMerchantCategoryAsync_Should_Return_False_For_CrossTenant()
    {
        await using var context = NewContext();
        var rule = new AccountTransactionMerchantCategory
        {
            TenantId = OtherTenantId,
            MerchantKey = "netflix",
            Category = "subscriptions",
        };
        context.AccountTransactionMerchantCategories.Add(rule);
        await context.SaveChangesAsync();
        var sut = NewManager(context);

        var result = await sut.DeleteMerchantCategoryAsync(rule.Id, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RecategorizeAsync_Should_Map_Legacy_PlaidStrings_To_Chronicle_Codes()
    {
        await using var context = NewContext();
        var connectionId = Guid.NewGuid();
        context.AccountConnections.Add(new AccountConnection
        {
            Id = connectionId,
            TenantId = TenantId,
            Provider = "Plaid",
            InstitutionName = "Test Bank",
            ProviderConnectionReference = "ref",
            SecretReference = "vault://x",
            Status = "Connected",
            ConsentStatus = "Granted",
        });
        await context.SaveChangesAsync();

        Seed(context, category: "MEDICAL", connectionId: connectionId);
        Seed(context, category: "FOOD_AND_DRINK", connectionId: connectionId);
        var sut = NewManager(context);

        var result = await sut.RecategorizeAsync(
            connectionId,
            new RecategorizeAccountTransactionsRequest(IncludeLocked: false, UnresolvedOnly: true),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Processed.Should().Be(2);
        result.Updated.Should().Be(2);

        var rows = await context.AccountTransactions
            .Where(t => t.AccountConnectionId == connectionId)
            .ToListAsync();
        rows.Should().AllSatisfy(r =>
        {
            r.CategoryMethod.Should().Be("provider_mapped");
            r.Category.Should().NotBe("MEDICAL");
            r.Category.Should().NotBe("FOOD_AND_DRINK");
        });
    }

    [Fact]
    public async Task RecategorizeAsync_Should_Return_Null_For_Unknown_Connection()
    {
        await using var context = NewContext();
        var sut = NewManager(context);

        var result = await sut.RecategorizeAsync(
            Guid.NewGuid(),
            new RecategorizeAccountTransactionsRequest(),
            CancellationToken.None);

        result.Should().BeNull();
    }
}
