namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>Additional execution-context checks before a classified mutation reaches its approval gate.</summary>
public interface IAgentMutationGuard
{
    Task ValidateAsync(CancellationToken cancellationToken = default);

    Task<ToolGateContext> PrepareAsync(ToolGateContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(context);
}
