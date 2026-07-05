using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.SharedKernel.Abstractions.Finance.Categorization;

namespace Aonik.PersonalFinance.Services.Accounts;

internal sealed class AccountTransactionCategorizer : IAccountTransactionCategorizer
{
    internal const string MethodProviderMapped = "provider_mapped";
    internal const string MethodMerchantRule = "merchant_rule";
    internal const string MethodManual = "manual";
    internal const string MethodFallback = "fallback";

    internal const decimal ConfidenceManual = 1.00m;
    internal const decimal ConfidenceMerchantRule = 0.90m;
    internal const decimal ConfidenceProviderSpecific = 0.85m;
    internal const decimal ConfidenceProviderOther = 0.40m;
    internal const decimal ConfidenceFallback = 0.00m;

    private readonly IChronicleCategoryMapper _mapper;

    public AccountTransactionCategorizer(IChronicleCategoryMapper mapper)
    {
        _mapper = mapper;
    }

    public void Classify(
        AccountTransaction transaction,
        AccountLinkProviderTransactionResult providerTransaction,
        AccountTransactionMerchantCategory? merchantRule)
    {
        // Step 1 — respect manual locks
        if (transaction.CategoryLockedAt is not null)
        {
            return;
        }

        // Step 2 — provider mapping
        var hadProviderData = !string.IsNullOrWhiteSpace(providerTransaction.Category);
        if (hadProviderData)
        {
            var mapping = _mapper.MapPlaidCategory(
                providerTransaction.Category,
                providerTransaction.SubCategory);

            if (mapping.Category is not null && mapping.Category != ChronicleCategoryCodes.Other)
            {
                Apply(transaction, mapping.Category, mapping.SubCategory, MethodProviderMapped, ConfidenceProviderSpecific);
                return;
            }
        }

        // Step 3 — merchant memory
        if (merchantRule is not null)
        {
            Apply(
                transaction,
                merchantRule.Category,
                merchantRule.SubCategory,
                MethodMerchantRule,
                ConfidenceMerchantRule);
            return;
        }

        // Step 4 — fallback: "other" if we had provider data, "uncategorized" otherwise
        if (hadProviderData)
        {
            Apply(
                transaction,
                ChronicleCategoryCodes.Other,
                null,
                MethodProviderMapped,
                ConfidenceProviderOther);
        }
        else
        {
            Apply(
                transaction,
                ChronicleCategoryCodes.Uncategorized,
                null,
                MethodFallback,
                ConfidenceFallback);
        }
    }

    private static void Apply(
        AccountTransaction tx,
        string category,
        string? subCategory,
        string method,
        decimal confidence)
    {
        tx.Category = category;
        tx.SubCategory = subCategory;
        tx.CategoryMethod = method;
        tx.CategoryConfidence = confidence;
    }
}
