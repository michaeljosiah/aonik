namespace Aonik.Application.Abstractions.Ai;

public interface IAgentRuntime
{
    Task<TOutput> ExecuteAsync<TInput, TOutput>(string agentName, TInput input, CancellationToken cancellationToken = default);
}
