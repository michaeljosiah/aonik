using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.PersonalFinance.Services.Accounts;
using FluentAssertions;

namespace Aonik.Application.Tests.Finance.Accounts;

public class AccountTransactionCategorizerTests
{
    private static AccountLinkProviderTransactionResult ProviderTx(
        string? category,
        string? subCategory = null,
        string? merchant = null,
        string? description = null)
    {
        return new AccountLinkProviderTransactionResult(
            ProviderTransactionReference: "tx-1",
            ProviderAccountReference: "acc-1",
            OccurredAt: DateTime.UtcNow,
            Amount: -10m,
            Currency: "USD",
            Merchant: merchant,
            Description: description,
            Category: category,
            SubCategory: subCategory,
            Pending: false);
    }

    [Fact]
    public void Classify_Should_NoOp_When_CategoryLockedAt_IsSet()
    {
        var sut = new AccountTransactionCategorizer(new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper());
        var tx = new AccountTransaction
        {
            Category = "manual_value",
            SubCategory = "manual_sub",
            CategoryMethod = "manual",
            CategoryConfidence = 1.00m,
            CategoryLockedAt = DateTime.UtcNow,
        };

        sut.Classify(tx, ProviderTx("FOOD_AND_DRINK", "RESTAURANT"), null);

        tx.Category.Should().Be("manual_value");
        tx.SubCategory.Should().Be("manual_sub");
        tx.CategoryMethod.Should().Be("manual");
        tx.CategoryConfidence.Should().Be(1.00m);
    }

    [Fact]
    public void Classify_Should_MapPlaid_When_DetailedCategoryKnown()
    {
        var sut = new AccountTransactionCategorizer(new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper());
        var tx = new AccountTransaction();

        sut.Classify(tx, ProviderTx("FOOD_AND_DRINK", "RESTAURANT"), null);

        tx.Category.Should().Be("eating_out");
        tx.SubCategory.Should().Be("restaurant");
        tx.CategoryMethod.Should().Be("provider_mapped");
        tx.CategoryConfidence.Should().Be(0.85m);
    }

    [Fact]
    public void Classify_Should_MapPlaid_PrimaryOnly_When_DetailedUnknown()
    {
        var sut = new AccountTransactionCategorizer(new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper());
        var tx = new AccountTransaction();

        sut.Classify(tx, ProviderTx("MEDICAL"), null);

        tx.Category.Should().Be("health");
        tx.SubCategory.Should().BeNull();
        tx.CategoryMethod.Should().Be("provider_mapped");
        tx.CategoryConfidence.Should().Be(0.85m);
    }

    [Fact]
    public void Classify_Should_UseMerchantRule_When_PlaidReturnsOther()
    {
        var sut = new AccountTransactionCategorizer(new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper());
        var tx = new AccountTransaction();
        var rule = new AccountTransactionMerchantCategory
        {
            Category = "subscriptions",
            SubCategory = "music",
        };

        // Plaid OTHER -> Other via PlaidPrimaryCategoryMap; merchant rule should win
        sut.Classify(tx, ProviderTx("OTHER", merchant: "Spotify"), rule);

        tx.Category.Should().Be("subscriptions");
        tx.SubCategory.Should().Be("music");
        tx.CategoryMethod.Should().Be("merchant_rule");
        tx.CategoryConfidence.Should().Be(0.90m);
    }

    [Fact]
    public void Classify_Should_PreferMerchantRule_Over_ProviderOther()
    {
        var sut = new AccountTransactionCategorizer(new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper());
        var tx = new AccountTransaction();
        var rule = new AccountTransactionMerchantCategory
        {
            Category = "bills",
            SubCategory = "insurance",
        };

        // GENERAL_SERVICES alone (no detailed) maps to Other via PlaidPrimaryCategoryMap
        sut.Classify(tx, ProviderTx("GENERAL_SERVICES"), rule);

        tx.Category.Should().Be("bills");
        tx.SubCategory.Should().Be("insurance");
        tx.CategoryMethod.Should().Be("merchant_rule");
    }

    [Fact]
    public void Classify_Should_FallbackToOther_When_PlaidOther_NoMerchantRule()
    {
        var sut = new AccountTransactionCategorizer(new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper());
        var tx = new AccountTransaction();

        sut.Classify(tx, ProviderTx("OTHER"), null);

        tx.Category.Should().Be("other");
        tx.SubCategory.Should().BeNull();
        tx.CategoryMethod.Should().Be("provider_mapped");
        tx.CategoryConfidence.Should().Be(0.40m);
    }

    [Fact]
    public void Classify_Should_FallbackToUncategorized_When_NoProviderData()
    {
        var sut = new AccountTransactionCategorizer(new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper());
        var tx = new AccountTransaction();

        sut.Classify(tx, ProviderTx(category: null), null);

        tx.Category.Should().Be("uncategorized");
        tx.SubCategory.Should().BeNull();
        tx.CategoryMethod.Should().Be("fallback");
        tx.CategoryConfidence.Should().Be(0.00m);
    }
}
