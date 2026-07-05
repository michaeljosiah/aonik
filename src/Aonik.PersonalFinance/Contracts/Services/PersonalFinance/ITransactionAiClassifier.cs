using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Contracts.Services;

/// <summary>
/// AI-powered transaction classifier used as a fallback when deterministic rules
/// do not match. Uses an LLM via <see cref="Microsoft.Extensions.AI.IChatClient"/>
/// to classify transactions into the canonical 26-category taxonomy.
/// </summary>
public interface ITransactionAiClassifier
{
    /// <summary>
    /// Classifies a single transaction using LLM inference.
    /// Returns <c>true</c> if the classifier assigned a category; <c>false</c> if it
    /// could not confidently classify (caller should fall back to Uncategorized).
    /// The method mutates the transaction entity directly (Category, SubCategory,
    /// Confidence, CategorisedBy, ClassificationMethod, ClassifierVersion, AiRunId).
    /// </summary>
    Task<bool> ClassifyAsync(
        PersonalTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Classifies a batch of transactions using LLM inference (single call for efficiency).
    /// Returns the number of transactions that were successfully classified.
    /// Transactions that cannot be classified are left unchanged.
    /// </summary>
    Task<int> ClassifyBatchAsync(
        IReadOnlyList<PersonalTransaction> transactions,
        CancellationToken cancellationToken = default);
}
