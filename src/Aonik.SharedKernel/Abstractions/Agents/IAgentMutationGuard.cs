namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>Additional execution-context checks before a classified mutation reaches its approval gate.</summary>
public interface IAgentMutationGuard
{
    Task ValidateAsync(CancellationToken cancellationToken = default);
}
