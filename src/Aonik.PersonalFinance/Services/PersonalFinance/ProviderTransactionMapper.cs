using System.Security.Cryptography;
using System.Text;

using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Shared static helpers for mapping provider transaction data onto
/// <see cref="PersonalTransaction"/> entities. Extracted from duplicated
/// private methods in <c>PersonalAccountLinkService</c> and
/// <c>FinancialConnectionTransactionSyncOrchestrator</c>.
/// </summary>
internal static class ProviderTransactionMapper
{
    /// <summary>
    /// Applies core fields from a provider sync result onto a <see cref="PersonalTransaction"/>,
    /// including amount, merchant, description, category, subcategory, and transaction type.
    /// Provider categorisation is only applied if no higher-confidence classification
    /// (manual, rule-based) is already present.
    /// </summary>
    public static void ApplyProviderTransaction(
        PersonalTransaction transaction,
        AccountLinkProviderTransactionResult providerTransaction)
    {
        transaction.OccurredAt = providerTransaction.OccurredAt;
        transaction.Amount = providerTransaction.Amount;
        transaction.Currency = providerTransaction.Currency.Trim().ToUpperInvariant();
        transaction.Merchant = TrimNullable(providerTransaction.Merchant);
        transaction.Description = TrimNullable(providerTransaction.Description);

        if (CanApplyProviderCategorisation(transaction))
        {
            transaction.Category = TrimNullable(providerTransaction.Category);
            transaction.SubCategory = TrimNullable(providerTransaction.SubCategory);

            if (!string.IsNullOrWhiteSpace(transaction.Category))
            {
                transaction.Confidence = 0.55m;
                transaction.CategorisedBy = "provider";
                transaction.ClassificationMethod = "provider";
                transaction.ReviewStatus = "Pending";
                transaction.ReviewedAt = null;
                transaction.ReviewedByUserId = null;
            }
            else
            {
                transaction.Confidence = 0m;
                transaction.CategorisedBy = null;
                transaction.ClassificationMethod = null;
                transaction.ReviewStatus = "Pending";
                transaction.ReviewedAt = null;
                transaction.ReviewedByUserId = null;
            }
        }

        transaction.TransactionType = TransactionCategoryReference.ResolveTransactionType(
            transaction.Category, transaction.Amount);
    }

    /// <summary>
    /// Returns <c>true</c> if the transaction's current classification is eligible to be
    /// overwritten by a provider-sourced category (i.e. no classification, or already provider-classified).
    /// </summary>
    public static bool CanApplyProviderCategorisation(PersonalTransaction transaction)
    {
        return string.IsNullOrWhiteSpace(transaction.ClassificationMethod)
            || string.Equals(transaction.ClassificationMethod, "provider", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transaction.CategorisedBy, "provider", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a deterministic <see cref="Guid"/> from a string value using MD5 hashing.
    /// Used to derive stable SourceId values from provider transaction references.
    /// </summary>
    public static Guid CreateDeterministicGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return new Guid(bytes);
    }

    /// <summary>
    /// Applies "ActionRequired" state to a connection and its linked/personal accounts.
    /// </summary>
    public static void ApplyActionRequiredState(
        FinancialConnection connection,
        IReadOnlyList<PersonalLinkedAccount> linkedAccounts,
        IReadOnlyList<PersonalAccount> personalAccounts,
        string syncStatus,
        string message)
    {
        connection.Status = "ActionRequired";
        connection.ConsentStatus = "ActionRequired";
        connection.LastSyncStatus = syncStatus;
        connection.LastError = LimitText(message, 1000);
        connection.NextScheduledSyncAt = null;

        foreach (var linkedAccount in linkedAccounts)
        {
            linkedAccount.Status = "ActionRequired";
            linkedAccount.LastSyncStatus = syncStatus;
            linkedAccount.LastError = LimitText(message, 1000);
        }

        foreach (var personalAccount in personalAccounts)
        {
            personalAccount.Status = "ActionRequired";
        }
    }

    /// <summary>
    /// Applies connected state to a personal account, optionally restoring it
    /// from archived state if appropriate.
    /// </summary>
    public static void ApplyConnectedPersonalAccountState(
        PersonalAccount personalAccount,
        PersonalLinkedAccount? linkedAccount,
        DateTime? previousDisconnectedAt,
        string connectedStatus = "Connected")
    {
        if (!personalAccount.IsArchived)
        {
            personalAccount.Status = connectedStatus;
            return;
        }

        if (!ShouldRestoreArchivedPersonalAccount(personalAccount, linkedAccount, previousDisconnectedAt))
        {
            return;
        }

        personalAccount.Status = connectedStatus;
        personalAccount.IsArchived = false;
        personalAccount.ClosedAt = null;
    }

    /// <summary>
    /// Determines whether an archived personal account should be automatically restored
    /// when a linked account reconnects.
    /// </summary>
    public static bool ShouldRestoreArchivedPersonalAccount(
        PersonalAccount personalAccount,
        PersonalLinkedAccount? linkedAccount,
        DateTime? previousDisconnectedAt)
    {
        return personalAccount.IsArchived
            && previousDisconnectedAt.HasValue
            && personalAccount.ClosedAt == previousDisconnectedAt
            && string.Equals(personalAccount.Status, "Archived", StringComparison.OrdinalIgnoreCase)
            && linkedAccount != null
            && string.Equals(linkedAccount.Status, "Archived", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trims whitespace from a nullable string, returning <c>null</c> if the result is empty.
    /// </summary>
    public static string? TrimNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Trims and truncates a nullable string to the specified maximum length.
    /// </summary>
    public static string? LimitText(string? value, int maxLength)
    {
        var normalized = TrimNullable(value);
        if (normalized == null)
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
