using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface ITransactionClassificationService
{
    Task<CategorisationRuleResponse> CreateRuleAsync(
        CreateCategorisationRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategorisationRuleResponse>> ListRulesAsync(
        CancellationToken cancellationToken = default);

    Task<CategorisationRuleResponse> UpdateRuleAsync(
        Guid ruleId,
        UpdateCategorisationRuleRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateRuleAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassificationReviewItemResponse>> GetReviewQueueAsync(
        ClassificationReviewQueueRequest request,
        CancellationToken cancellationToken = default);

    Task<ClassificationReviewItemResponse> AcceptClassificationAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<ClassificationReviewItemResponse> OverrideClassificationAsync(
        Guid transactionId,
        OverrideTransactionClassificationRequest request,
        CancellationToken cancellationToken = default);
}
