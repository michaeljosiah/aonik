using Aonik.Ai.Contracts.Models;

namespace Aonik.Ai.Contracts.Services;

public interface IPromptSpecService
{
    Task<IReadOnlyList<PromptSpecResponse>> ListAsync(string? name = null, CancellationToken ct = default);
    Task<PromptSpecResponse?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PromptSpecResponse> CreateAsync(CreatePromptSpecRequest request, CancellationToken ct = default);
    Task<PromptSpecResponse> UpdateAsync(Guid id, UpdatePromptSpecRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IRoutePolicyService
{
    Task<IReadOnlyList<RoutePolicyResponse>> ListAsync(string? useCase = null, CancellationToken ct = default);
    Task<RoutePolicyResponse?> GetAsync(Guid id, CancellationToken ct = default);
    Task<RoutePolicyResponse> CreateAsync(CreateRoutePolicyRequest request, CancellationToken ct = default);
    Task<RoutePolicyResponse> UpdateAsync(Guid id, UpdateRoutePolicyRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
