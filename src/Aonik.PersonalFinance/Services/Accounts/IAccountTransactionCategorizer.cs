using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;

namespace Aonik.PersonalFinance.Services.Accounts;

/// <summary>
/// Classifies an <see cref="AccountTransaction"/> against the Chronicle taxonomy.
/// Pipeline (first match wins):
///   1. lock check (skip if <see cref="AccountTransaction.CategoryLockedAt"/> is set)
///   2. provider mapping via <c>TransactionCategoryReference.MapPlaidCategoryWithSubCategory</c>
///   3. tenant merchant memory rule
///   4. fallback ("other" with provider data, "uncategorized" without)
/// </summary>
internal interface IAccountTransactionCategorizer
{
    void Classify(
        AccountTransaction transaction,
        AccountLinkProviderTransactionResult providerTransaction,
        AccountTransactionMerchantCategory? merchantRule);
}
