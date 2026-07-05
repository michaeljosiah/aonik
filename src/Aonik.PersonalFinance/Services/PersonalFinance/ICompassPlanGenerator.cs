using Aonik.PersonalFinance.Agents.StructuredOutputs;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Internal seam over the <c>pf-compass-planner</c> sub-agent invocation
/// (Spec 021 §5). Lets <c>CompassPlanService</c> own the persistence + AiRun
/// lifecycle while keeping the LLM call (descriptor resolution + IChatClient +
/// structured-output parsing) isolated and stubbable in unit tests.
/// </summary>
internal interface ICompassPlanGenerator
{
    Task<CompassPlannerAgentToolResponse> GenerateAsync(
        CompassPlannerRequest request,
        CancellationToken cancellationToken = default);
}
